using SGSClient.Core.Enums;
using SGSClient.Core.Models;

namespace SGSClient.Core.Interfaces;

public interface IInstallQueueService
{
    IReadOnlyList<InstallQueueEntry> Entries { get; }

    event EventHandler? Changed;

    event EventHandler<InstallQueueProgressEventArgs>? ProgressChanged;

    Task EnqueueInstallAsync(int gameId, string? version = null, CancellationToken cancellationToken = default);

    Task EnqueueUpdateAsync(int gameId, CancellationToken cancellationToken = default);

    void Cancel(int gameId);

    bool IsQueuedOrActive(int gameId);

    /// <summary>
    /// Compares installed versions with catalog and enqueues updates when auto-update is enabled.
    /// </summary>
    Task CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}

public sealed class InstallQueueEntry
{
    public required int GameId { get; init; }
    public string? Version { get; init; }
    public bool IsUpdate { get; init; }
    public InstallState State { get; set; } = InstallState.Queued;
    public string? Title { get; set; }
    public string? Error { get; set; }
}

public sealed class InstallQueueProgressEventArgs : EventArgs
{
    public required int GameId { get; init; }
    public required InstallProgress Progress { get; init; }
    public InstallState State { get; init; }
}
