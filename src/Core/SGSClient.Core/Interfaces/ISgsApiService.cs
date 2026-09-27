using System.Net.Http.Headers;
using System.Net.Http.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;

namespace SGSClient.Core.Interfaces;

public interface ISgsApiService
{
    Task<IReadOnlyList<GameDto>> GetGamesAsync(
        GameCatalogQuery? query = null,
        CancellationToken cancellationToken = default);
    Task<GameDto?> GetGameAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDlcDto>> GetGameDlcsAsync(int gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDto>> GetMyGamesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDto>> GetMyLibraryAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> ClaimGameAsync(int gameId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFromLibraryAsync(int gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDto>> GetWishlistAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> AddToWishlistAsync(int gameId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFromWishlistAsync(int gameId, CancellationToken cancellationToken = default);
    Task<bool> SendPresenceAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> UpdateGameAsync(int gameId, UpdateGameRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteGameAsync(int gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDto>> GetAdminGamesAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> UpdateAdminGameAsync(int gameId, UpdateGameRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAdminGameAsync(int gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default);
    Task<AdminRoleUpdateResult> UpdateAdminUserRoleAsync(
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default);
    Task<GameDto?> UploadGameCoverAsync(int gameId, string localFilePath, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> ClearGameCoverAsync(int gameId, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> UploadGameHeaderAsync(int gameId, string localFilePath, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> ClearGameHeaderAsync(int gameId, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> UploadGameLogoAsync(int gameId, string localFilePath, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> ClearGameLogoAsync(int gameId, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> UploadGameIntroAsync(int gameId, string localFilePath, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> ClearGameIntroAsync(int gameId, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> AddGameScreenshotAsync(int gameId, string localFilePath, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> RemoveGameScreenshotAsync(int gameId, int screenshotId, bool asAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameBuildVersionDto>> GetBuildVersionsAsync(
        int gameId,
        int buildId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameBuildVersionDto>> GetOwnedVersionsAsync(
        int gameId,
        CancellationToken cancellationToken = default);

    Task<GameStatsDto?> GetOwnedGameStatsAsync(int gameId, CancellationToken cancellationToken = default);

    Task<GameBuildVersionDto?> UpdateVersionNotesAsync(
        int gameId,
        int versionId,
        string? releaseNotes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameBuildVersionDto>> GetAdminVersionsAsync(
        int gameId,
        CancellationToken cancellationToken = default);

    Task<GameStatsDto?> GetAdminGameStatsAsync(int gameId, CancellationToken cancellationToken = default);

    Task<GameBuildVersionDto?> UpdateAdminVersionNotesAsync(
        int gameId,
        int versionId,
        string? releaseNotes,
        CancellationToken cancellationToken = default);

    Task<GameReviewsSummaryDto?> GetGameReviewsAsync(int gameId, CancellationToken cancellationToken = default);

    Task<(GameReviewDto? Review, string? Error)> UpsertGameReviewAsync(
        int gameId,
        UpsertGameReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteGameReviewAsync(int gameId, CancellationToken cancellationToken = default);
}
