using System.Net.Http.Headers;
using System.Net.Http.Json;
using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface ISgsApiService
{
    Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> GetGameAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDlcDto>> GetGameDlcsAsync(int gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDto>> GetMyGamesAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> UpdateGameAsync(int gameId, UpdateGameRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteGameAsync(int gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameDto>> GetAdminGamesAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> UpdateAdminGameAsync(int gameId, UpdateGameRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAdminGameAsync(int gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> UploadGameCoverAsync(int gameId, string localFilePath, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> ClearGameCoverAsync(int gameId, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> UploadGameIntroAsync(int gameId, string localFilePath, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> ClearGameIntroAsync(int gameId, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> AddGameScreenshotAsync(int gameId, string localFilePath, bool asAdmin, CancellationToken cancellationToken = default);
    Task<GameDto?> RemoveGameScreenshotAsync(int gameId, int screenshotId, bool asAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameBuildVersionDto>> GetBuildVersionsAsync(
        int gameId,
        int buildId,
        CancellationToken cancellationToken = default);
}
