using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;

namespace SGSClient.UI.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly IInstallQueueService? _queue;
    private readonly IClientUserSettingsStore? _userSettings;
    private readonly object _progressGate = new();
    private readonly Dictionary<int, double> _progress = new();
    private readonly Dictionary<int, string?> _messages = new();
    private readonly SynchronizationContext? _ui;
    private readonly string? _apiBaseUrl;
    private Action<int>? _openDetails;
    private bool _suppressBandwidthPersist;

    // Speed is smoothed over the progress events of the active game; it restarts when another game takes over.
    private int _speedGameId = -1;
    private long _speedLastBytes;
    private long _speedLastTick;
    private double _speedBytesPerSecond;
    private double _peakBytesPerSecond;

    public DownloadsViewModel()
        : this(null, null)
    {
    }

    public DownloadsViewModel(
        IInstallQueueService? queue,
        IClientUserSettingsStore? userSettings = null,
        string? apiBaseUrl = null)
    {
        _queue = queue;
        _userSettings = userSettings;
        _apiBaseUrl = apiBaseUrl;
        _ui = SynchronizationContext.Current;
        if (_queue is not null)
        {
            _queue.Changed += (_, _) => PostToUi(Refresh);
            _queue.ProgressChanged += OnProgress;
        }

        _suppressBandwidthPersist = true;
        try
        {
            DownloadMaxKbps = _userSettings?.Load().DownloadMaxKbps ?? 0;
        }
        finally
        {
            _suppressBandwidthPersist = false;
        }

        Refresh();
    }

    public ObservableCollection<DownloadRowItem> Items { get; } = [];

    /// <summary>Everything in <see cref="Items"/> except the download shown in the header.</summary>
    public ObservableCollection<DownloadRowItem> UpNext { get; } = [];

    [ObservableProperty]
    private DownloadRowItem? _activeRow;

    [ObservableProperty]
    private bool _hasUpNext;

    [ObservableProperty]
    private string? _activeCoverUrl;

    [ObservableProperty]
    private string _activeSpeedText = "—";

    [ObservableProperty]
    private string _activePeakText = "—";

    [ObservableProperty]
    private string _activeSizeText = string.Empty;

    [ObservableProperty]
    private string _activeEtaText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private bool _hasActiveDownload;

    [ObservableProperty]
    private string _activeDownloadTitle = string.Empty;

    [ObservableProperty]
    private string _activeDownloadDetail = string.Empty;

    [ObservableProperty]
    private double _activeDownloadProgress;

    [ObservableProperty]
    private string _activeDownloadProgressText = string.Empty;

    [ObservableProperty]
    private int _activeDownloadCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadMaxMbps))]
    private int _downloadMaxKbps;

    /// <summary>The limit as shown to the user; GameInstallService counts 1 MB/s as 1024 KB/s.</summary>
    public double DownloadMaxMbps
    {
        get => Math.Round(DownloadMaxKbps / 1024.0, 2);
        set
        {
            // Pole odsyła zaokrągloną wartość; bez tej kontroli samo wyświetlenie zmieniałoby zapisany limit.
            if (value != DownloadMaxMbps)
            {
                DownloadMaxKbps = (int)Math.Round(Math.Max(0, value) * 1024);
            }
        }
    }

    partial void OnDownloadMaxKbpsChanged(int value)
    {
        if (_suppressBandwidthPersist || _userSettings is null)
        {
            return;
        }

        var settings = _userSettings.Load();
        settings.DownloadMaxKbps = Math.Max(0, value);
        _ = _userSettings.SaveAsync(settings);
    }

    public void SetOpenDetailsHandler(Action<int> openDetails) => _openDetails = openDetails;

    public void Refresh() => RefreshList();

    [RelayCommand]
    private void RefreshList()
    {
        if (_queue is null)
        {
            Items.Clear();
            HasItems = false;
            IsEmpty = true;
            StatusText = Loc.T("Kolejka instalacji niedostępna.");
            return;
        }

        var active = _queue.Entries
            .Where(e => e.State is InstallState.Queued or InstallState.Paused
                or InstallState.Downloading or InstallState.Installing
                or InstallState.Updating or InstallState.Failed)
            .OrderBy(e => e.State switch
            {
                InstallState.Downloading or InstallState.Installing or InstallState.Updating => 0,
                InstallState.Queued => 1,
                InstallState.Paused => 2,
                _ => 3
            })
            .ThenBy(e => e.SortOrder)
            .ThenBy(e => e.GameId)
            .ToList();

        Items.Clear();
        for (var i = 0; i < active.Count; i++)
        {
            var entry = active[i];
            double fraction;
            string? message;
            lock (_progressGate)
            {
                _progress.TryGetValue(entry.GameId, out fraction);
                _messages.TryGetValue(entry.GameId, out message);
            }

            var canReorder = entry.State is InstallState.Queued or InstallState.Paused;
            var queuedIndex = active
                .Where(e => e.State is InstallState.Queued or InstallState.Paused)
                .ToList()
                .FindIndex(e => e.GameId == entry.GameId);
            var queuedCount = active.Count(e => e.State is InstallState.Queued or InstallState.Paused);
            Items.Add(DownloadRowItem.From(
                entry,
                fraction,
                message,
                canMoveUp: canReorder && queuedIndex > 0,
                canMoveDown: canReorder && queuedIndex >= 0 && queuedIndex < queuedCount - 1,
                coverImageUrl: ResolveCover(entry.CoverUrl)));
        }

        HasItems = Items.Count > 0;
        IsEmpty = !HasItems;
        StatusText = HasItems
            ? $"{Items.Count} w kolejce"
            : Loc.T("Brak aktywnych pobierań.");

        ActiveDownloadCount = Items.Count;
        var primary = Items.FirstOrDefault(i => i.ShowProgress) ?? Items.FirstOrDefault();
        HasActiveDownload = primary is not null;
        ActiveRow = primary;
        ActiveCoverUrl = primary?.CoverImageUrl;
        UpNext.Clear();
        foreach (var item in Items.Where(i => !ReferenceEquals(i, primary)))
        {
            UpNext.Add(item);
        }

        HasUpNext = UpNext.Count > 0;
        if (primary is null || !primary.ShowProgress)
        {
            ResetSpeed();
        }

        if (primary is null)
        {
            ActiveDownloadTitle = string.Empty;
            ActiveDownloadDetail = string.Empty;
            ActiveDownloadProgress = 0;
            ActiveDownloadProgressText = string.Empty;
            return;
        }

        ActiveDownloadTitle = primary.Title;
        ActiveDownloadDetail = Items.Count > 1
            ? Loc.F("{0} · +{1} w kolejce", primary.StateText, Items.Count - 1)
            : $"{primary.KindText} · {primary.StateText}";
        ActiveDownloadProgress = primary.Progress;
        ActiveDownloadProgressText = primary.ShowProgress
            ? primary.ProgressText
            : primary.StateText;
    }

    [RelayCommand]
    private void Cancel(DownloadRowItem? row)
    {
        if (row is null || _queue is null)
        {
            return;
        }

        _queue.Cancel(row.GameId);
        Refresh();
    }

    [RelayCommand]
    private void Pause(DownloadRowItem? row)
    {
        if (row is null || _queue is null)
        {
            return;
        }

        _queue.Pause(row.GameId);
        Refresh();
    }

    [RelayCommand]
    private void Resume(DownloadRowItem? row)
    {
        if (row is null || _queue is null)
        {
            return;
        }

        _queue.Resume(row.GameId);
        Refresh();
    }

    [RelayCommand]
    private void MoveUp(DownloadRowItem? row)
    {
        if (row is null || _queue is null)
        {
            return;
        }

        _queue.MoveUp(row.GameId);
        Refresh();
    }

    [RelayCommand]
    private void MoveDown(DownloadRowItem? row)
    {
        if (row is null || _queue is null)
        {
            return;
        }

        _queue.MoveDown(row.GameId);
        Refresh();
    }

    [RelayCommand]
    private void OpenDetails(DownloadRowItem? row)
    {
        if (row is null)
        {
            return;
        }

        _openDetails?.Invoke(row.GameId);
    }

    private void OnProgress(object? sender, InstallQueueProgressEventArgs e)
    {
        PostToUi(() => ApplyProgress(e));
    }

    private void ApplyProgress(InstallQueueProgressEventArgs e)
    {
        lock (_progressGate)
        {
            _progress[e.GameId] = e.Progress.Fraction;
            _messages[e.GameId] = e.Progress.Message;
            if (e.Progress.Phase is InstallPhase.Completed or InstallPhase.Failed)
            {
                _progress.Remove(e.GameId);
                _messages.Remove(e.GameId);
            }
        }

        // State transitions rebuild the list; pure progress only patches the existing row
        // so the UI thread is not flooded by Clear/Add on every download chunk.
        if (e.Progress.Phase is InstallPhase.Completed or InstallPhase.Failed
            || e.State is InstallState.Installing or InstallState.Failed or InstallState.Installed)
        {
            Refresh();
            return;
        }

        var row = Items.FirstOrDefault(i => i.GameId == e.GameId);
        if (row is null)
        {
            Refresh();
            return;
        }

        UpdateSpeed(e);
        var pct = Math.Clamp(e.Progress.Fraction, 0, 1);
        row.Progress = pct;
        row.ProgressText = $"{pct * 100:0}%";
        row.ShowProgress = true;
        if (!string.IsNullOrWhiteSpace(e.Progress.Message))
        {
            row.DetailText = e.Progress.Message;
        }

        if (Items.FirstOrDefault(i => i.ShowProgress)?.GameId == e.GameId || Items[0].GameId == e.GameId)
        {
            ActiveDownloadProgress = pct;
            ActiveDownloadProgressText = row.ProgressText;
            if (!string.IsNullOrWhiteSpace(row.Title))
            {
                ActiveDownloadTitle = row.Title;
            }
        }
    }

    private void UpdateSpeed(InstallQueueProgressEventArgs e)
    {
        if (e.Progress.Phase != InstallPhase.Downloading || e.Progress.TotalBytes <= 0
            || ActiveRow is null || ActiveRow.GameId != e.GameId)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var received = e.Progress.BytesReceived;
        if (_speedGameId != e.GameId || received < _speedLastBytes)
        {
            ResetSpeed();
            _speedGameId = e.GameId;
            _speedLastBytes = received;
            _speedLastTick = now;
        }
        else
        {
            var seconds = Stopwatch.GetElapsedTime(_speedLastTick, now).TotalSeconds;
            if (seconds >= 0.5)
            {
                var instant = (received - _speedLastBytes) / seconds;
                _speedBytesPerSecond = _speedBytesPerSecond <= 0 ? instant : _speedBytesPerSecond * 0.7 + instant * 0.3;
                _peakBytesPerSecond = Math.Max(_peakBytesPerSecond, _speedBytesPerSecond);
                _speedLastBytes = received;
                _speedLastTick = now;
            }
        }

        ActiveSpeedText = _speedBytesPerSecond > 0 ? FormatSpeed(_speedBytesPerSecond) : "—";
        ActivePeakText = _peakBytesPerSecond > 0 ? FormatSpeed(_peakBytesPerSecond) : "—";
        ActiveSizeText = $"{FormatSize(received)} / {FormatSize(e.Progress.TotalBytes)}";
        ActiveEtaText = _speedBytesPerSecond > 0
            ? Loc.F("Pozostało około {0}", FormatDuration((e.Progress.TotalBytes - received) / _speedBytesPerSecond))
            : string.Empty;
    }

    private void ResetSpeed()
    {
        _speedGameId = -1;
        _speedBytesPerSecond = 0;
        _peakBytesPerSecond = 0;
        ActiveSpeedText = "—";
        ActivePeakText = "—";
        ActiveSizeText = string.Empty;
        ActiveEtaText = string.Empty;
    }

    private string? ResolveCover(string? raw)
    {
        var url = _apiBaseUrl is null ? raw : InstallPathHelper.TryResolveDownloadUrl(_apiBaseUrl, raw);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "file"
            ? uri.AbsoluteUri
            : null;
    }

    internal static string FormatSize(long bytes) => bytes >= 1L << 30
        ? $"{bytes / (double)(1L << 30):0.##} GB"
        : $"{bytes / (double)(1L << 20):0.#} MB";

    internal static string FormatSpeed(double bytesPerSecond) => $"{bytesPerSecond / (1L << 20):0.#} MB/s";

    internal static string FormatDuration(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1 ? span.ToString(@"h\:mm\:ss") : span.ToString(@"mm\:ss");
    }

    private void PostToUi(Action action)
    {
        if (_ui is null || ReferenceEquals(SynchronizationContext.Current, _ui))
        {
            action();
            return;
        }

        _ui.Post(_ => action(), null);
    }
}

public partial class DownloadRowItem : ObservableObject
{
    public required int GameId { get; init; }
    public required string Title { get; init; }
    public required string KindText { get; init; }
    public required string StateText { get; init; }
    public required bool CanCancel { get; init; }
    public required bool CanPause { get; init; }
    public required bool CanResume { get; init; }
    public required bool CanMoveUp { get; init; }
    public required bool CanMoveDown { get; init; }
    public string? CoverImageUrl { get; init; }

    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial string ProgressText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool ShowProgress { get; set; }
    [ObservableProperty] public partial string? DetailText { get; set; }

    public static DownloadRowItem From(
        InstallQueueEntry entry,
        double fraction,
        string? message,
        bool canMoveUp = false,
        bool canMoveDown = false,
        string? coverImageUrl = null)
    {
        var showProgress = entry.State is InstallState.Downloading or InstallState.Installing
            or InstallState.Updating;
        var canCancel = entry.State is InstallState.Queued or InstallState.Paused
            or InstallState.Downloading or InstallState.Installing or InstallState.Updating;
        var canPause = entry.State is InstallState.Queued or InstallState.Downloading
            or InstallState.Installing or InstallState.Updating;
        var canResume = entry.State == InstallState.Paused;
        var pct = showProgress ? Math.Clamp(fraction, 0, 1) : 0;
        return new DownloadRowItem
        {
            GameId = entry.GameId,
            Title = string.IsNullOrWhiteSpace(entry.Title) ? Loc.T("Gra bez tytułu") : entry.Title!,
            KindText = entry.IsUpdate ? Loc.T("Aktualizacja") : Loc.T("Instalacja"),
            StateText = entry.State switch
            {
                InstallState.Queued => Loc.T("W kolejce"),
                InstallState.Paused => Loc.T("Wstrzymano"),
                InstallState.Downloading => Loc.T("Pobieranie"),
                InstallState.Installing => Loc.T("Instalowanie"),
                InstallState.Updating => Loc.T("Aktualizowanie"),
                InstallState.Failed => Loc.T("Błąd"),
                InstallState.Installed => Loc.T("Zainstalowano"),
                _ => "—"
            },
            Progress = pct,
            ProgressText = showProgress ? $"{pct * 100:0}%" : string.Empty,
            CanCancel = canCancel,
            CanPause = canPause,
            CanResume = canResume,
            CanMoveUp = canMoveUp,
            CanMoveDown = canMoveDown,
            CoverImageUrl = coverImageUrl,
            ShowProgress = showProgress,
            DetailText = !string.IsNullOrWhiteSpace(entry.Error)
                ? entry.Error
                : message
        };
    }
}
