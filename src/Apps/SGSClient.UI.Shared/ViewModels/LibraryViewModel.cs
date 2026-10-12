using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Core.Playtime;

namespace SGSClient.UI.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly IGameLauncherService? _launcher;
    private readonly IGameInstallService? _installer;
    private readonly IInstallQueueService? _queue;
    private readonly ILibraryDiscoveryService? _discovery;
    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl;
    private Action<LibraryGameItem>? _openProperties;
    private Action<int>? _openDetails;

    public LibraryViewModel()
        : this(null)
    {
    }

    public LibraryViewModel(
        IServiceScopeFactory? scopes,
        IGameLauncherService? launcher = null,
        IGameInstallService? installer = null,
        ILibraryDiscoveryService? discovery = null,
        ISgsApiService? api = null,
        IConfiguration? configuration = null,
        IInstallQueueService? queue = null,
        IAuthSession? auth = null)
    {
        _scopes = scopes;
        _launcher = launcher;
        _installer = installer;
        _discovery = discovery;
        _api = api;
        _queue = queue;
        _auth = auth;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = Loc.T("Ładowanie biblioteki…");

        if (_queue is not null)
        {
            _queue.Changed += (_, _) => _ = SyncQueueProgressAsync();
            _queue.ProgressChanged += OnQueueProgress;
        }
    }

    public void SetOpenPropertiesHandler(Action<LibraryGameItem> open) => _openProperties = open;
    public void SetOpenDetailsHandler(Action<int> open) => _openDetails = open;

    public string Title { get; } = Loc.T("Biblioteka");

    public ObservableCollection<LibraryGameItem> Games { get; } = [];
    public ObservableCollection<LibraryGameItem> FilteredGames { get; } = [];
    public ObservableCollection<LibraryGameItem> RecentGames { get; } = [];
    public ObservableCollection<LibraryGameItem> AllGames { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasGames { get; set; }

    [ObservableProperty]
    public partial bool ShowEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial LibraryGameItem? SelectedGame { get; set; }

    [ObservableProperty]
    public partial bool IsHomeSelected { get; set; } = true;

    public bool ShowHome => HasGames;
    public string AllGamesHeader => Loc.F("Wszystkie gry ({0})", AllGames.Count);
    public bool ShowRecent => RecentGames.Count > 0;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnHasGamesChanged(bool value) => OnPropertyChanged(nameof(ShowHome));

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_scopes is null)
        {
            StatusText = Loc.T("Brak magazynu lokalnego.");
            ShowEmpty = true;
            HasGames = false;
            return;
        }

        IsBusy = true;
        try
        {
            if (_discovery is not null)
            {
                using var scanScope = _scopes.CreateScope();
                var scanner = scanScope.ServiceProvider.GetRequiredService<ILibraryDiscoveryService>();
                await scanner.ScanAsync();
            }

            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            await SyncEntitlementsAsync(library);
            var items = await library.GetAllAsync();
            var playtime = scope.ServiceProvider.GetService<IPlaytimeStore>();
            if (playtime is not null)
            {
                await playtime.SyncFromServerAsync();
                await playtime.ApplyToLibraryAsync(items);
            }

            Games.Clear();
            foreach (var item in items.OrderBy(x => x.Title))
            {
                Games.Add(LibraryGameItem.FromModel(item, _apiBaseUrl));
            }

            await EnrichCoversAsync(library);
            RebuildShelves();
            ApplyFilter();
            await CheckUpdatesOnStartupAsync();

            HasGames = Games.Count > 0;
            ShowEmpty = !HasGames;
            StatusText = HasGames
                ? $"{Games.Count} gier w bibliotece"
                : Loc.T("Biblioteka jest pusta. Dodaj gry ze Sklepu.");

            if (SelectedGame is not null)
            {
                SelectedGame = Games.FirstOrDefault(g => g.GameId == SelectedGame.GameId);
                if (SelectedGame is null)
                {
                    IsHomeSelected = true;
                }
            }
        }
        catch (Exception ex)
        {
            HasGames = false;
            ShowEmpty = true;
            StatusText = Loc.F("Błąd biblioteki: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Runs auto-update scan (safe to call at app startup without opening Library).</summary>
    public async Task CheckUpdatesOnStartupAsync()
    {
        if (_queue is null)
        {
            return;
        }

        try
        {
            await _queue.CheckForUpdatesAsync();
            await SyncQueueProgressAsync();
        }
        catch
        {
            // best-effort at startup
        }
    }

    private async Task SyncEntitlementsAsync(ILocalLibraryStore library)
    {
        if (_api is null || (_auth is { IsAvailable: true } && !_auth.IsLoggedIn))
        {
            return;
        }

        try
        {
            var remote = await _api.GetMyLibraryAsync();
            var local = await library.GetAllAsync();
            var remoteById = remote.ToDictionary(g => g.Id);
            var localById = local.ToDictionary(x => x.GameId);

            foreach (var game in remote)
            {
                GameBuildDto? build = null;
                if (game.Builds.Count > 0)
                {
                    var preferredPlatform = OperatingSystem.IsWindows() ? GamePlatform.Windows
                        : OperatingSystem.IsLinux() ? GamePlatform.Linux
                        : OperatingSystem.IsMacOS() ? GamePlatform.MacOS
                        : GamePlatform.Windows;
                    build = game.Builds.FirstOrDefault(b => b.Platform == preferredPlatform && b.IsDefault)
                        ?? game.Builds.FirstOrDefault(b => b.Platform == preferredPlatform)
                        ?? game.Builds.FirstOrDefault(b => b.IsDefault)
                        ?? game.Builds[0];
                }

                var buildId = build?.Id ?? 0;
                var platform = build?.Platform ?? default;
                var architecture = build?.Architecture;
                var targetVersion = build?.CurrentVersion;

                if (localById.TryGetValue(game.Id, out var existing))
                {
                    var nextBuildId = buildId > 0 ? buildId : existing.BuildId;
                    var changed = existing.Title != game.Title
                        || existing.CoverUrl != game.ThumbnailUrl
                        || existing.LogoUrl != game.LogoUrl
                        || existing.Symbol != game.Symbol
                        || (buildId > 0 && existing.BuildId != buildId)
                        || (targetVersion is not null && existing.TargetVersion != targetVersion);

                    if (!changed)
                    {
                        continue;
                    }

                    await library.UpsertAsync(new GameLibraryItem
                    {
                        GameId = existing.GameId,
                        BuildId = nextBuildId,
                        Symbol = game.Symbol,
                        Title = game.Title,
                        Platform = build?.Platform ?? existing.Platform,
                        Architecture = architecture ?? existing.Architecture,
                        InstallState = existing.InstallState,
                        InstallPath = existing.InstallPath,
                        ExecutablePath = existing.ExecutablePath,
                        InstalledVersion = existing.InstalledVersion,
                        TargetVersion = targetVersion ?? existing.TargetVersion,
                        CoverUrl = game.ThumbnailUrl ?? existing.CoverUrl,
                        LogoUrl = game.LogoUrl ?? existing.LogoUrl,
                        CustomCoverPath = existing.CustomCoverPath,
                        LastPlayedUtc = existing.LastPlayedUtc
                    });
                    continue;
                }

                await library.UpsertAsync(new GameLibraryItem
                {
                    GameId = game.Id,
                    BuildId = buildId,
                    Symbol = game.Symbol,
                    Title = game.Title,
                    Platform = platform,
                    Architecture = architecture,
                    InstallState = InstallState.NotInstalled,
                    TargetVersion = targetVersion,
                    CoverUrl = game.ThumbnailUrl,
                    LogoUrl = game.LogoUrl
                });
            }

            foreach (var item in local)
            {
                if (remoteById.ContainsKey(item.GameId))
                {
                    continue;
                }

                if (item.InstallState is InstallState.Installed
                    or InstallState.Downloading
                    or InstallState.Installing
                    or InstallState.Updating
                    or InstallState.Queued)
                {
                    continue;
                }

                await library.RemoveAsync(item.GameId);
            }
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Sync biblioteki: {0}", ex.Message);
        }
    }

    private void OnQueueProgress(object? sender, InstallQueueProgressEventArgs e)
    {
        var row = Games.FirstOrDefault(g => g.GameId == e.GameId);
        if (row is null)
        {
            return;
        }

        row.InstallProgressFraction = e.Progress.Fraction;
        row.InstallProgressText = e.Progress.Message ?? string.Empty;
        row.ApplyInstallState(e.State);
        row.IsInstalling = e.State is InstallState.Queued or InstallState.Downloading
            or InstallState.Installing or InstallState.Updating;
    }

    private async Task SyncQueueProgressAsync()
    {
        if (_queue is null || _scopes is null)
        {
            return;
        }

        foreach (var row in Games)
        {
            row.IsInstalling = _queue.IsQueuedOrActive(row.GameId);
            if (!row.IsInstalling)
            {
                row.InstallProgressFraction = 0;
                row.InstallProgressText = string.Empty;
            }
        }

        foreach (var entry in _queue.Entries)
        {
            var row = Games.FirstOrDefault(g => g.GameId == entry.GameId);
            if (row is null)
            {
                continue;
            }

            row.ApplyInstallState(entry.State);
            row.IsInstalling = entry.State is InstallState.Queued or InstallState.Downloading
                or InstallState.Installing or InstallState.Updating;
            if (!string.IsNullOrWhiteSpace(entry.Error))
            {
                StatusText = entry.Error;
            }
        }

        // Refresh finished / cancelled installs from store
        if (_queue.Entries.Any(e => e.State is InstallState.Installed or InstallState.Failed or InstallState.NotInstalled))
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            foreach (var entry in _queue.Entries.Where(e =>
                         e.State is InstallState.Installed or InstallState.Failed or InstallState.NotInstalled))
            {
                var model = await library.GetByGameIdAsync(entry.GameId);
                var row = Games.FirstOrDefault(g => g.GameId == entry.GameId);
                if (model is not null && row is not null)
                {
                    row.ApplyModel(model, _apiBaseUrl);
                    row.IsInstalling = false;
                }
            }

            RebuildShelves();
        }
    }

    [RelayCommand]
    private void CancelInstall(LibraryGameItem? item)
    {
        item ??= SelectedGame;
        if (item is null || _queue is null || !item.IsInstalling)
        {
            return;
        }

        _queue.Cancel(item.GameId);
        item.IsInstalling = false;
        item.InstallProgressFraction = 0;
        item.InstallProgressText = string.Empty;
        StatusText = $"Anulowano: {item.Title}";
        _ = SyncQueueProgressAsync();
    }

    [RelayCommand]
    private void SelectHome()
    {
        IsHomeSelected = true;
        SelectedGame = null;
    }

    [RelayCommand]
    private void SelectGame(LibraryGameItem? item)
    {
        if (item is null)
        {
            return;
        }

        IsHomeSelected = false;
        SelectedGame = item;
        _openDetails?.Invoke(item.GameId);
    }

    [RelayCommand]
    private void OpenProperties(LibraryGameItem? item)
    {
        var target = item ?? SelectedGame;
        if (target is null)
        {
            return;
        }

        _openProperties?.Invoke(target);
    }

    [RelayCommand]
    private async Task InstallAsync(LibraryGameItem? item)
    {
        item ??= SelectedGame;
        if (item is null || _scopes is null || item.IsInstalling)
        {
            return;
        }

        if (_queue is not null)
        {
            item.IsInstalling = true;
            item.ApplyInstallState(InstallState.Queued);
            StatusText = $"W kolejce: {item.Title}";
            await _queue.EnqueueInstallAsync(item.GameId, item.TargetVersion);
            return;
        }

        item.IsInstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var installer = _installer ?? scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var model = await library.GetByGameIdAsync(item.GameId);
            if (model is null)
            {
                StatusText = Loc.T("Nie znaleziono gry w bibliotece.");
                return;
            }

            var targetVersion = model.TargetVersion;
            model.InstallState = InstallState.Downloading;
            await library.UpsertAsync(model);
            item.ApplyModel(model, _apiBaseUrl);

            var progress = new Progress<InstallProgress>(p =>
            {
                item.InstallProgressFraction = p.Fraction;
                item.InstallProgressText = p.Message ?? string.Empty;
                if (p.Phase is InstallPhase.Downloading)
                {
                    item.ApplyInstallState(InstallState.Downloading);
                }
                else if (p.Phase is InstallPhase.Installing)
                {
                    item.ApplyInstallState(InstallState.Installing);
                }
            });

            var result = await installer.InstallAsync(model, version: targetVersion, progress: progress);
            await library.UpsertAsync(model);
            item.ApplyModel(model, _apiBaseUrl);
            RebuildShelves();

            StatusText = result.Success
                ? string.IsNullOrWhiteSpace(targetVersion)
                    ? Loc.F("Zainstalowano: {0} (najnowsza).", model.Title)
                    : Loc.F("Zainstalowano: {0} (v{1}).", model.Title, targetVersion)
                : result.Errors.FirstOrDefault() ?? Loc.T("Instalacja nie powiodła się.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Instalacja nie powiodła się: {0}", ex.Message);
        }
        finally
        {
            item.IsInstalling = false;
            item.InstallProgressFraction = 0;
            item.InstallProgressText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task UpdateAsync(LibraryGameItem? item)
    {
        item ??= SelectedGame;
        if (item is null || _queue is null)
        {
            return;
        }

        StatusText = Loc.F("Aktualizacja w kolejce: {0}", item.Title);
        await _queue.EnqueueUpdateAsync(item.GameId);
    }

    [RelayCommand]
    private async Task UninstallAsync(LibraryGameItem? item)
    {
        item ??= SelectedGame;
        if (item is null || _scopes is null || item.IsInstalling)
        {
            return;
        }

        item.IsInstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var installer = _installer ?? scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var model = await library.GetByGameIdAsync(item.GameId);
            if (model is null)
            {
                StatusText = Loc.T("Nie znaleziono gry w bibliotece.");
                return;
            }

            var result = await installer.UninstallAsync(model);
            await library.UpsertAsync(model);
            item.ApplyModel(model, _apiBaseUrl);
            RebuildShelves();

            StatusText = result.Success
                ? Loc.F("Odinstalowano: {0} (gra zostaje w bibliotece).", model.Title)
                : result.Errors.FirstOrDefault() ?? Loc.T("Odinstalowanie nie powiodło się.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Odinstalowanie nie powiodło się: {0}", ex.Message);
        }
        finally
        {
            item.IsInstalling = false;
        }
    }

    [RelayCommand]
    private async Task LaunchAsync(LibraryGameItem? item)
    {
        item ??= SelectedGame;
        if (item is null || _launcher is null || string.IsNullOrWhiteSpace(item.ExecutablePath))
        {
            StatusText = item is null
                ? Loc.T("Brak gry.")
                : Loc.T("Gra nie jest zainstalowana. Kliknij Zainstaluj.");
            return;
        }

        try
        {
            var result = await _launcher.LaunchAsync(new GameLaunchRequest
            {
                GameId = item.GameId,
                Title = item.Title,
                ExecutablePath = item.ExecutablePath
            });
            StatusText = result.Message;
            if (_scopes is not null)
            {
                using var scope = _scopes.CreateScope();
                var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
                var existing = await library.GetByGameIdAsync(item.GameId);
                if (existing is not null)
                {
                    var playtime = scope.ServiceProvider.GetService<IPlaytimeStore>();
                    if (playtime is not null)
                    {
                        await playtime.ApplyToLibraryAsync([existing]);
                    }

                    item.ApplyModel(existing, _apiBaseUrl);
                    RebuildShelves();
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się uruchomić: {0}", ex.Message);
        }
    }

    private async Task EnrichCoversAsync(ILocalLibraryStore library)
    {
        if (_api is null)
        {
            return;
        }

        foreach (var row in Games.Where(g => string.IsNullOrWhiteSpace(g.CoverUrl)))
        {
            try
            {
                var game = await _api.GetGameAsync(row.GameId);
                if (string.IsNullOrWhiteSpace(game?.ThumbnailUrl) && string.IsNullOrWhiteSpace(game?.LogoUrl))
                {
                    continue;
                }

                var model = await library.GetByGameIdAsync(row.GameId);
                if (model is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(game.ThumbnailUrl))
                {
                    model.CoverUrl = game.ThumbnailUrl;
                }

                model.LogoUrl = game.LogoUrl;
                await library.UpsertAsync(model);
                row.ApplyModel(model, _apiBaseUrl);
            }
            catch
            {
                // best-effort
            }
        }

        foreach (var row in Games)
        {
            try
            {
                var model = await library.GetByGameIdAsync(row.GameId);
                if (model is null || !string.IsNullOrWhiteSpace(model.LogoUrl))
                {
                    continue;
                }

                var game = await _api.GetGameAsync(row.GameId);
                if (string.IsNullOrWhiteSpace(game?.LogoUrl))
                {
                    continue;
                }

                model.LogoUrl = game.LogoUrl;
                await library.UpsertAsync(model);
                row.ApplyModel(model, _apiBaseUrl);
            }
            catch
            {
                // best-effort
            }
        }
    }

    private void RebuildShelves()
    {
        RecentGames.Clear();
        foreach (var game in Games
                     .Where(g => g.LastPlayedUtc is not null)
                     .OrderByDescending(g => g.LastPlayedUtc)
                     .Take(8))
        {
            RecentGames.Add(game);
        }

        AllGames.Clear();
        foreach (var game in Games.OrderBy(g => g.Title))
        {
            AllGames.Add(game);
        }

        OnPropertyChanged(nameof(AllGamesHeader));
        OnPropertyChanged(nameof(ShowRecent));
    }

    private void ApplyFilter()
    {
        FilteredGames.Clear();
        IEnumerable<LibraryGameItem> query = Games;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(g =>
                g.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || g.Symbol.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var game in query.OrderBy(g => g.Title))
        {
            FilteredGames.Add(game);
        }
    }
}

public partial class LibraryGameItem : ObservableObject
{
    public required int GameId { get; init; }
    public required int BuildId { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }

    public DateTimeOffset? LastPlayedUtc { get; private set; }
    public string? CoverUrl { get; private set; }
    public string? TargetVersion { get; private set; }

    [ObservableProperty]
    public partial string TileLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlatformText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ExecutablePath { get; set; }

    [ObservableProperty]
    public partial string? CoverImageUrl { get; set; }

    [ObservableProperty]
    public partial bool HasCover { get; set; }

    /// <summary>Portrait capsule for the library cards. The sidebar keeps <see cref="CoverImageUrl"/> (the logo).</summary>
    [ObservableProperty]
    public partial string? PosterImageUrl { get; set; }

    [ObservableProperty]
    public partial bool HasPoster { get; set; }

    [ObservableProperty]
    public partial bool CanLaunch { get; set; }

    [ObservableProperty]
    public partial bool CanInstall { get; set; }

    [ObservableProperty]
    public partial bool CanUninstall { get; set; }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    public double InstallProgressMaxHeight => IsInstalling ? 48 : 0;

    partial void OnIsInstallingChanged(bool value) =>
        OnPropertyChanged(nameof(InstallProgressMaxHeight));

    [ObservableProperty]
    public partial double InstallProgressFraction { get; set; }

    [ObservableProperty]
    public partial string InstallProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastPlayedText { get; set; } = Loc.T("Niegrana");

    [ObservableProperty]
    public partial string PlaytimeText { get; set; } = string.Empty;

    public bool HasPlaytime => !string.IsNullOrWhiteSpace(PlaytimeText);

    partial void OnPlaytimeTextChanged(string value) => OnPropertyChanged(nameof(HasPlaytime));

    public static LibraryGameItem FromModel(GameLibraryItem item, string apiBaseUrl)
    {
        var vm = new LibraryGameItem
        {
            GameId = item.GameId,
            BuildId = item.BuildId,
            Title = item.Title,
            Symbol = item.Symbol
        };
        vm.ApplyModel(item, apiBaseUrl);
        return vm;
    }

    public void ApplyModel(GameLibraryItem item, string apiBaseUrl)
    {
        TileLabel = string.IsNullOrWhiteSpace(item.Symbol)
            ? item.Title[..Math.Min(2, item.Title.Length)].ToUpperInvariant()
            : item.Symbol[..Math.Min(3, item.Symbol.Length)].ToUpperInvariant();
        PlatformText = item.Platform + (string.IsNullOrWhiteSpace(item.Architecture) ? "" : $" ({item.Architecture})");
        VersionText = string.IsNullOrWhiteSpace(item.InstalledVersion)
            ? (string.IsNullOrWhiteSpace(item.TargetVersion) ? "—" : Loc.F("Cel: {0}", item.TargetVersion))
            : item.InstalledVersion!;
        ExecutablePath = item.ExecutablePath;
        CoverUrl = item.CoverUrl;
        TargetVersion = item.TargetVersion;
        LastPlayedUtc = item.LastPlayedUtc;
        CoverImageUrl = ResolveLogoImage(item, apiBaseUrl);
        HasCover = !string.IsNullOrWhiteSpace(CoverImageUrl);
        PosterImageUrl = ResolvePosterImage(item, apiBaseUrl);
        HasPoster = !string.IsNullOrWhiteSpace(PosterImageUrl);
        LastPlayedText = item.LastPlayedUtc is null
            ? Loc.T("Niegrana")
            : item.LastPlayedUtc.Value.LocalDateTime.ToString("g");
        PlaytimeText = item.TotalPlayTimeSeconds > 0
            ? PlaytimeFormat.ToPolishTotal(item.TotalPlayTimeSeconds)
            : string.Empty;
        ApplyInstallState(item.InstallState);
    }

    private static string? ResolveLogoImage(GameLibraryItem item, string apiBaseUrl)
    {
        if (TryCustomCover(item, out var custom))
        {
            return custom;
        }

        var art = !string.IsNullOrWhiteSpace(item.LogoUrl) ? item.LogoUrl : item.CoverUrl;
        return InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, art);
    }

    private static string? ResolvePosterImage(GameLibraryItem item, string apiBaseUrl)
    {
        if (TryCustomCover(item, out var custom))
        {
            return custom;
        }

        var art = !string.IsNullOrWhiteSpace(item.CoverUrl) ? item.CoverUrl : item.LogoUrl;
        return InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, art);
    }

    private static bool TryCustomCover(GameLibraryItem item, out string? uri)
    {
        if (!string.IsNullOrWhiteSpace(item.CustomCoverPath) && File.Exists(item.CustomCoverPath))
        {
            uri = new Uri(item.CustomCoverPath).AbsoluteUri;
            return true;
        }

        uri = null;
        return false;
    }

    public void ApplyInstallState(InstallState state)
    {
        StateText = state switch
        {
            InstallState.Installed => Loc.T("Zainstalowana"),
            InstallState.NotInstalled => "W bibliotece (niezainstalowana)",
            InstallState.Downloading => Loc.T("Pobieranie…"),
            InstallState.Installing => Loc.T("Instalacja…"),
            InstallState.Failed => Loc.T("Błąd instalacji"),
            InstallState.Updating => Loc.T("Aktualizacja…"),
            InstallState.Queued => Loc.T("W kolejce"),
            _ => state.ToString()
        };

        CanLaunch = state == InstallState.Installed
            && !string.IsNullOrWhiteSpace(ExecutablePath);
        CanInstall = state is InstallState.NotInstalled or InstallState.Failed;
        CanUninstall = state == InstallState.Installed;
    }
}
