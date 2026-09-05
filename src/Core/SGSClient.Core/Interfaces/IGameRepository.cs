using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IGameRepository
{
    Task<IReadOnlyList<GameDto>> GetPublishedGamesAsync(
        GameCatalogQuery? query = null,
        CancellationToken cancellationToken = default);
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

    /// <summary>Every published version across all builds, including drafts, for the owner's panel.</summary>
    Task<IReadOnlyList<GameBuildVersionDto>> GetOwnedVersionHistoryAsync(
        int gameId,
        int ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Version history for any game — admin catalog editor.</summary>
    Task<IReadOnlyList<GameBuildVersionDto>> GetVersionHistoryAsync(
        int gameId,
        CancellationToken cancellationToken = default);

    Task<GameBuildVersionDto?> UpdateOwnedVersionNotesAsync(
        int gameId,
        int versionId,
        int ownerUserId,
        string? releaseNotes,
        CancellationToken cancellationToken = default);

    Task<GameBuildVersionDto?> UpdateVersionNotesAsAdminAsync(
        int gameId,
        int versionId,
        string? releaseNotes,
        CancellationToken cancellationToken = default);

    Task<GameStatsDto?> GetOwnedGameStatsAsync(
        int gameId,
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task<GameStatsDto?> GetGameStatsAsync(
        int gameId,
        CancellationToken cancellationToken = default);

    /// <summary>Records one package download for the creator statistics.</summary>
    Task RecordDownloadAsync(
        string symbol,
        string version,
        string platformKey,
        CancellationToken cancellationToken = default);
}
