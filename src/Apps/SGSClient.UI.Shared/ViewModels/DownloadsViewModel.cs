using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;

namespace SGSClient.UI.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly IInstallQueueService? _queue;
    private readonly IClientUserSettingsStore? _userSettings;
    private readonly Dictionary<int, double> _progress = new();
    private readonly Dictionary<int, string?> _messages = new();
    private Action<int>? _openDetails;
    private bool _suppressBandwidthPersist;

    public DownloadsViewModel()
        : this(null, null)
    {
    }

    public DownloadsViewModel(IInstallQueueService? queue, IClientUserSettingsStore? userSettings = null)
    {
        _queue = queue;
        _userSettings = userSettings;
        if (_queue is not null)
        {
            _queue.Changed += (_, _) => Refresh();
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
    private int _downloadMaxKbps;

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
            StatusText = "Kolejka instalacji niedostępna.";
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
            _progress.TryGetValue(entry.GameId, out var fraction);
            _messages.TryGetValue(entry.GameId, out var message);
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
                canMoveDown: canReorder && queuedIndex >= 0 && queuedIndex < queuedCount - 1));
        }

        HasItems = Items.Count > 0;
        IsEmpty = !HasItems;
        StatusText = HasItems
            ? $"{Items.Count} w kolejce"
            : "Brak aktywnych pobierań.";

        ActiveDownloadCount = Items.Count;
        var primary = Items.FirstOrDefault(i => i.ShowProgress) ?? Items.FirstOrDefault();
        HasActiveDownload = primary is not null;
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
            ? $"{primary.StateText} · +{Items.Count - 1} w kolejce"
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
        _progress[e.GameId] = e.Progress.Fraction;
        _messages[e.GameId] = e.Progress.Message;
        if (e.Progress.Phase is InstallPhase.Completed or InstallPhase.Failed)
        {
            _progress.Remove(e.GameId);
        }

        Refresh();
    }
}

public partial class DownloadRowItem : ObservableObject
{
    public required int GameId { get; init; }
    public required string Title { get; init; }
    public required string KindText { get; init; }
    public required string StateText { get; init; }
    public required double Progress { get; init; }
    public required string ProgressText { get; init; }
    public required bool CanCancel { get; init; }
    public required bool CanPause { get; init; }
    public required bool CanResume { get; init; }
    public required bool CanMoveUp { get; init; }
    public required bool CanMoveDown { get; init; }
    public required bool ShowProgress { get; init; }
    public string? DetailText { get; init; }

    public static DownloadRowItem From(
        InstallQueueEntry entry,
        double fraction,
        string? message,
        bool canMoveUp = false,
        bool canMoveDown = false)
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
            Title = string.IsNullOrWhiteSpace(entry.Title) ? $"Gra #{entry.GameId}" : entry.Title!,
            KindText = entry.IsUpdate ? "Aktualizacja" : "Instalacja",
            StateText = entry.State switch
            {
                InstallState.Queued => "W kolejce",
                InstallState.Paused => "Wstrzymano",
                InstallState.Downloading => "Pobieranie",
                InstallState.Installing => "Instalowanie",
                InstallState.Updating => "Aktualizowanie",
                InstallState.Failed => "Błąd",
                _ => entry.State.ToString()
            },
            Progress = pct,
            ProgressText = showProgress ? $"{pct * 100:0}%" : string.Empty,
            CanCancel = canCancel,
            CanPause = canPause,
            CanResume = canResume,
            CanMoveUp = canMoveUp,
            CanMoveDown = canMoveDown,
            ShowProgress = showProgress,
            DetailText = !string.IsNullOrWhiteSpace(entry.Error)
                ? entry.Error
                : message
        };
    }
}
