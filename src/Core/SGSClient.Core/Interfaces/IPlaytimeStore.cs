using SGSClient.Core.Models;

namespace SGSClient.Core.Interfaces;

public interface IPlaytimeStore
{
    Task<PlaytimeRecord?> GetAsync(int gameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaytimeRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task RecordSessionAsync(
        int gameId,
        TimeSpan duration,
        DateTimeOffset endedAtUtc,
        CancellationToken cancellationToken = default);

    Task SyncFromServerAsync(CancellationToken cancellationToken = default);

    Task ApplyToLibraryAsync(
        IReadOnlyList<GameLibraryItem> items,
        CancellationToken cancellationToken = default);
}
