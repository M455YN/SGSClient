using SGSClient.Core.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Persistence.Entities;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class InstallQueueService : IInstallQueueService, IDisposable
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ClientInstallSettings> _installOptions;
    private readonly IClientUserSettingsStore _userSettings;
    private readonly List<InstallQueueEntry> _entries = [];
    private readonly object _gate = new();
    private readonly SemaphoreSlim _workerLock = new(1, 1);
    private CancellationTokenSource? _activeCts;
    private int? _activeGameId;
    private bool _pumpRunning;
    private bool _pauseRequested;
    private SynchronizationContext? _uiContext;
    private EventHandler? _changed;
    private EventHandler<InstallQueueProgressEventArgs>? _progressChanged;
    private long _lastProgressUiTick;
    private double _lastProgressFraction = -1;
    private int _lastProgressGameId = -1;

    public InstallQueueService(
        IServiceScopeFactory scopes,
        IOptions<ClientInstallSettings> installOptions,
        IClientUserSettingsStore userSettings)
    {
        _scopes = scopes;
        _installOptions = installOptions;
        _userSettings = userSettings;
    }

    public IReadOnlyList<InstallQueueEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries
                    .OrderBy(e => e.SortOrder)
                    .ThenBy(e => e.GameId)
                    .ToList();
            }
        }
    }

    public event EventHandler? Changed
    {
        add
        {
            CaptureUiContext();
            _changed += value;
        }
        remove => _changed -= value;
    }

    public event EventHandler<InstallQueueProgressEventArgs>? ProgressChanged
    {
        add
        {
            CaptureUiContext();
            _progressChanged += value;
        }
        remove => _progressChanged -= value;
    }

    public async Task RestorePersistedAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
        var rows = (await db.InstallQueue.AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.EnqueuedAtUtc)
            .ToList();

        foreach (var row in rows)
        {
            await EnqueueCoreAsync(
                    row.GameId,
                    row.Version,
                    row.IsUpdate,
                    persist: false,
                    paused: row.IsPaused,
                    sortOrder: row.SortOrder)
                .ConfigureAwait(false);
        }
    }

    public async Task EnqueueInstallAsync(
        int gameId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnqueueCoreAsync(gameId, version, isUpdate: false).ConfigureAwait(false);
    }

    public async Task EnqueueUpdateAsync(int gameId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnqueueCoreAsync(gameId, version: null, isUpdate: true).ConfigureAwait(false);
    }

    public void Cancel(int gameId)
    {
        lock (_gate)
        {
            if (_activeGameId == gameId)
            {
                _pauseRequested = false;
                _activeCts?.Cancel();
            }

            foreach (var entry in _entries.Where(e =>
                         e.GameId == gameId
                         && e.State is InstallState.Queued or InstallState.Paused))
            {
                entry.State = InstallState.NotInstalled;
                entry.Error = "Anulowano.";
            }

            _entries.RemoveAll(e =>
                e.GameId == gameId
                && e.State is InstallState.NotInstalled or InstallState.Queued or InstallState.Paused);
        }

        _ = RemovePersistedAsync(gameId);
        RaiseChanged();
        _ = EnsurePumpAsync();
    }

    public void Pause(int gameId)
    {
        lock (_gate)
        {
            if (_activeGameId == gameId)
            {
                _pauseRequested = true;
                _activeCts?.Cancel();
                return;
            }

            var entry = _entries.FirstOrDefault(e =>
                e.GameId == gameId && e.State == InstallState.Queued);
            if (entry is null)
            {
                return;
            }

            entry.State = InstallState.Paused;
            entry.Error = null;
        }

        _ = PersistPausedAsync(gameId, paused: true);
        RaiseChanged();
    }

    public void Resume(int gameId)
    {
        lock (_gate)
        {
            var entry = _entries.FirstOrDefault(e =>
                e.GameId == gameId && e.State == InstallState.Paused);
            if (entry is null)
            {
                return;
            }

            entry.State = InstallState.Queued;
            entry.Error = null;
        }

        _ = PersistPausedAsync(gameId, paused: false);
        RaiseChanged();
        _ = EnsurePumpAsync();
    }

    public void MoveUp(int gameId)
    {
        lock (_gate)
        {
            var queued = _entries
                .Where(e => e.State is InstallState.Queued or InstallState.Paused)
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.GameId)
                .ToList();
            var index = queued.FindIndex(e => e.GameId == gameId);
            if (index <= 0)
            {
                return;
            }

            (queued[index - 1].SortOrder, queued[index].SortOrder) =
                (queued[index].SortOrder, queued[index - 1].SortOrder);
            if (queued[index - 1].SortOrder == queued[index].SortOrder)
            {
                queued[index - 1].SortOrder--;
            }
        }

        _ = PersistSortOrdersAsync();
        RaiseChanged();
    }

    public void MoveDown(int gameId)
    {
        lock (_gate)
        {
            var queued = _entries
                .Where(e => e.State is InstallState.Queued or InstallState.Paused)
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.GameId)
                .ToList();
            var index = queued.FindIndex(e => e.GameId == gameId);
            if (index < 0 || index >= queued.Count - 1)
            {
                return;
            }

            (queued[index].SortOrder, queued[index + 1].SortOrder) =
                (queued[index + 1].SortOrder, queued[index].SortOrder);
            if (queued[index].SortOrder == queued[index + 1].SortOrder)
            {
                queued[index + 1].SortOrder++;
            }
        }

        _ = PersistSortOrdersAsync();
        RaiseChanged();
    }

    public bool IsQueuedOrActive(int gameId)
    {
        lock (_gate)
        {
            return _entries.Any(e =>
                e.GameId == gameId
                && e.State is InstallState.Queued or InstallState.Paused
                    or InstallState.Downloading or InstallState.Installing or InstallState.Updating);
        }
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsAutoUpdateEnabled())
        {
            return;
        }

        using var scope = _scopes.CreateScope();
        var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
        var api = scope.ServiceProvider.GetService<ISgsApiService>();
        if (api is null)
        {
            return;
        }

        var items = await library.GetOwnedAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in items.Where(x => x.InstallState == InstallState.Installed))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsQueuedOrActive(item.GameId))
            {
                continue;
            }

            try
            {
                var game = await api.GetGameAsync(item.GameId, cancellationToken).ConfigureAwait(false);
                var build = game?.Builds.FirstOrDefault(b => b.Id == item.BuildId)
                    ?? game?.Builds.FirstOrDefault(b => b.IsDefault)
                    ?? game?.Builds.FirstOrDefault();
                if (build is null || string.IsNullOrWhiteSpace(build.CurrentVersion))
                {
                    continue;
                }

                if (!string.Equals(item.InstalledVersion, build.CurrentVersion, StringComparison.OrdinalIgnoreCase))
                {
                    await EnqueueUpdateAsync(item.GameId, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                // best-effort auto-update scan
            }
        }
    }

    private async Task EnqueueCoreAsync(
        int gameId,
        string? version,
        bool isUpdate,
        bool persist = true,
        bool paused = false,
        int? sortOrder = null)
    {
        InstallQueueEntry entry;
        lock (_gate)
        {
            if (_entries.Any(e =>
                    e.GameId == gameId
                    && e.State is InstallState.Queued or InstallState.Paused
                        or InstallState.Downloading or InstallState.Installing or InstallState.Updating))
            {
                return;
            }

            var nextOrder = sortOrder
                ?? (_entries.Count == 0 ? 0 : _entries.Max(e => e.SortOrder) + 1);

            entry = new InstallQueueEntry
            {
                GameId = gameId,
                Version = version,
                IsUpdate = isUpdate,
                State = paused ? InstallState.Paused : InstallState.Queued,
                SortOrder = nextOrder
            };
            _entries.Add(entry);
        }

        if (persist)
        {
            await PersistAsync(entry).ConfigureAwait(false);
        }

        RaiseChanged();
        if (!paused)
        {
            await EnsurePumpAsync().ConfigureAwait(false);
        }
    }

    private async Task PersistAsync(InstallQueueEntry entry)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
            var existing = await db.InstallQueue.FirstOrDefaultAsync(x => x.GameId == entry.GameId)
                .ConfigureAwait(false);
            if (existing is null)
            {
                db.InstallQueue.Add(new LocalInstallQueueEntity
                {
                    GameId = entry.GameId,
                    Version = entry.Version,
                    IsUpdate = entry.IsUpdate,
                    Title = entry.Title,
                    EnqueuedAtUtc = DateTimeOffset.UtcNow,
                    SortOrder = entry.SortOrder,
                    IsPaused = entry.State == InstallState.Paused
                });
            }
            else
            {
                existing.Version = entry.Version;
                existing.IsUpdate = entry.IsUpdate;
                existing.Title = entry.Title;
                existing.SortOrder = entry.SortOrder;
                existing.IsPaused = entry.State == InstallState.Paused;
            }

            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // queue persistence is best-effort
        }
    }

    private async Task PersistPausedAsync(int gameId, bool paused)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
            var existing = await db.InstallQueue.FirstOrDefaultAsync(x => x.GameId == gameId)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return;
            }

            existing.IsPaused = paused;
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
    }

    private async Task PersistSortOrdersAsync()
    {
        List<(int GameId, int SortOrder)> snapshot;
        lock (_gate)
        {
            snapshot = _entries.Select(e => (e.GameId, e.SortOrder)).ToList();
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
            var rows = await db.InstallQueue.ToListAsync().ConfigureAwait(false);
            foreach (var row in rows)
            {
                var match = snapshot.FirstOrDefault(x => x.GameId == row.GameId);
                if (match.GameId == row.GameId)
                {
                    row.SortOrder = match.SortOrder;
                }
            }

            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
    }

    private async Task RemovePersistedAsync(int gameId)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
            var existing = await db.InstallQueue.FirstOrDefaultAsync(x => x.GameId == gameId)
                .ConfigureAwait(false);
            if (existing is null)
            {
                return;
            }

            db.InstallQueue.Remove(existing);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }
    }

    private async Task EnsurePumpAsync()
    {
        lock (_gate)
        {
            if (_pumpRunning)
            {
                return;
            }

            _pumpRunning = true;
        }

        _ = Task.Run(PumpAsync);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                InstallQueueEntry? entry;
                lock (_gate)
                {
                    entry = _entries
                        .Where(e => e.State == InstallState.Queued)
                        .OrderBy(e => e.SortOrder)
                        .ThenBy(e => e.GameId)
                        .FirstOrDefault();
                }

                if (entry is null)
                {
                    break;
                }

                await _workerLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Re-check still queued (may have been paused/cancelled)
                    lock (_gate)
                    {
                        if (entry.State != InstallState.Queued)
                        {
                            continue;
                        }
                    }

                    await ProcessEntryAsync(entry).ConfigureAwait(false);
                }
                finally
                {
                    _workerLock.Release();
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _pumpRunning = false;
                if (_entries.Any(e => e.State == InstallState.Queued))
                {
                    _pumpRunning = true;
                    _ = Task.Run(PumpAsync);
                }
            }
        }
    }

    private async Task ProcessEntryAsync(InstallQueueEntry entry)
    {
        using var cts = new CancellationTokenSource();
        lock (_gate)
        {
            _pauseRequested = false;
            _activeCts = cts;
            _activeGameId = entry.GameId;
            entry.State = entry.IsUpdate ? InstallState.Updating : InstallState.Downloading;
        }

        RaiseChanged();

        var wasPaused = false;
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var installer = scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var model = await library.GetByGameIdAsync(entry.GameId, cts.Token).ConfigureAwait(false);
            if (model is null)
            {
                entry.State = InstallState.Failed;
                entry.Error = Loc.T("Nie znaleziono gry w lokalnej bibliotece.");
                RaiseChanged();
                return;
            }

            entry.Title = model.Title;
            model.InstallState = entry.State;
            await library.UpsertAsync(model, cts.Token).ConfigureAwait(false);

            var progress = new Progress<InstallProgress>(p =>
            {
                var state = p.Phase switch
                {
                    InstallPhase.Downloading => entry.IsUpdate ? InstallState.Updating : InstallState.Downloading,
                    InstallPhase.Installing => InstallState.Installing,
                    InstallPhase.Completed => InstallState.Installed,
                    _ => entry.State
                };
                entry.State = state;
                RaiseProgress(new InstallQueueProgressEventArgs
                {
                    GameId = entry.GameId,
                    Progress = p,
                    State = state
                });
            });

            var result = await installer.InstallAsync(
                    model,
                    version: entry.IsUpdate ? null : entry.Version ?? model.TargetVersion,
                    progress: progress,
                    cancellationToken: cts.Token)
                .ConfigureAwait(false);

            await library.UpsertAsync(model, cts.Token).ConfigureAwait(false);
            entry.State = result.Success ? InstallState.Installed : InstallState.Failed;
            entry.Error = result.Success ? null : result.Errors.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                wasPaused = _pauseRequested;
            }

            if (wasPaused)
            {
                entry.State = InstallState.Paused;
                entry.Error = null;
            }
            else
            {
                entry.State = InstallState.NotInstalled;
                entry.Error = "Anulowano.";
            }
        }
        catch (Exception ex)
        {
            entry.State = InstallState.Failed;
            entry.Error = ex.Message;
        }
        finally
        {
            if (wasPaused)
            {
                await PersistAsync(entry).ConfigureAwait(false);
            }
            else
            {
                await RemovePersistedAsync(entry.GameId).ConfigureAwait(false);
                lock (_gate)
                {
                    if (entry.State is InstallState.Installed or InstallState.Failed or InstallState.NotInstalled)
                    {
                        _entries.Remove(entry);
                    }
                }
            }

            lock (_gate)
            {
                if (_activeGameId == entry.GameId)
                {
                    _activeGameId = null;
                    _activeCts = null;
                    _pauseRequested = false;
                }
            }

            RaiseChanged();
        }
    }

    private void CaptureUiContext()
    {
        var current = SynchronizationContext.Current;
        if (current is not null)
        {
            _uiContext ??= current;
        }
    }

    private void RaiseChanged() => PostToUi(() => _changed?.Invoke(this, EventArgs.Empty));

    private void RaiseProgress(InstallQueueProgressEventArgs args)
    {
        var force = args.Progress.Phase is not InstallPhase.Downloading
            || args.State is InstallState.Installing or InstallState.Failed or InstallState.Installed;
        var now = Environment.TickCount64;
        if (!force
            && args.GameId == _lastProgressGameId
            && now - _lastProgressUiTick < 100
            && Math.Abs(args.Progress.Fraction - _lastProgressFraction) < 0.005)
        {
            return;
        }

        _lastProgressUiTick = now;
        _lastProgressFraction = args.Progress.Fraction;
        _lastProgressGameId = args.GameId;
        PostToUi(() => _progressChanged?.Invoke(this, args));
    }

    private void PostToUi(Action action)
    {
        CaptureUiContext();
        var ctx = _uiContext;
        if (ctx is null || ReferenceEquals(SynchronizationContext.Current, ctx))
        {
            action();
            return;
        }

        // Always Post — never run heavy UI work inline on a worker that happens to
        // share affinity, and never Send (that would block the download thread).
        ctx.Post(_ => action(), null);
    }

    private bool IsAutoUpdateEnabled()
    {
        try
        {
            return _userSettings.Load().AutoUpdateEnabled;
        }
        catch
        {
            return _installOptions.Value.AutoUpdateEnabled;
        }
    }

    public void Dispose()
    {
        _activeCts?.Cancel();
        _activeCts?.Dispose();
        _workerLock.Dispose();
    }
}
