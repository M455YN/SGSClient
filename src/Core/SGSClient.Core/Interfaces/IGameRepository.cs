using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IGameRepository
{
    Task<IReadOnlyList<GameDto>> GetPublishedGamesAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> GetPublishedGameByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameBuildVersionDto>> GetBuildVersionHistoryAsync(
        int gameId,
        int buildId,
        CancellationToken cancellationToken = default);
}
