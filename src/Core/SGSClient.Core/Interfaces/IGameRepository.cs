using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IGameRepository
{
    Task<IReadOnlyList<GameDto>> GetPublishedGamesAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> GetPublishedGameByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDlcDto>> GetPublishedDlcsAsync(int parentGameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDto>> GetOwnedGamesAsync(int userId, CancellationToken cancellationToken = default);
    Task<GameDto?> UpdateOwnedGameAsync(int gameId, int userId, UpdateGameRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteOwnedGameAsync(int gameId, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDto>> GetAllGamesForAdminAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> UpdateGameAsAdminAsync(int gameId, UpdateGameRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteGameAsAdminAsync(int gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameBuildVersionDto>> GetBuildVersionHistoryAsync(
        int gameId,
        int buildId,
        CancellationToken cancellationToken = default);
}
