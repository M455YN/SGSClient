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

    /// <summary>Like <see cref="GetMyLibraryAsync"/>, but null when the request failed, so an empty library stays distinguishable.</summary>
    Task<IReadOnlyList<GameDto>?> TryGetMyLibraryAsync(CancellationToken cancellationToken = default);
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

    Task<GameReviewsSummaryDto?> GetGameReviewsAsync(
        int gameId,
        string? sort = null,
        bool? recommended = null,
        CancellationToken cancellationToken = default);

    Task<(GameReviewDto? Review, string? Error)> UpsertGameReviewAsync(
        int gameId,
        UpsertGameReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteGameReviewAsync(int gameId, CancellationToken cancellationToken = default);

    Task<bool> VoteGameReviewAsync(long reviewId, bool helpful, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameReviewCommentDto>> GetGameReviewCommentsAsync(
        long reviewId,
        CancellationToken cancellationToken = default);

    Task<(GameReviewCommentDto? Comment, string? Error)> AddGameReviewCommentAsync(
        long reviewId,
        string text,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteGameReviewCommentAsync(long commentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementDefinitionDto>> GetAchievementDefinitionsAsync(
        int gameId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AchievementDefinitionDto>? Items, string? Error)> SyncAchievementDefinitionsAsync(
        int gameId,
        IReadOnlyList<AchievementDefinitionDto> items,
        CancellationToken cancellationToken = default);

    /// <summary>Unlocks an achievement on the signed-in account, so a creator can test the flow.</summary>
    Task<(AchievementUpdateResultDto? Result, string? Error)> TestUnlockAchievementAsync(
        int gameId,
        string key,
        CancellationToken cancellationToken = default);

    Task<bool> ResetMyAchievementProgressAsync(int gameId, CancellationToken cancellationToken = default);

    /// <summary>Uploads an image as the icon of a saved achievement; returns the updated definition.</summary>
    Task<(AchievementDefinitionDto? Item, string? Error)> UploadAchievementIconAsync(
        int gameId,
        string key,
        string localFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>Player view with the signed-in user's progress; <paramref name="language"/> picks translations.</summary>
    Task<GameAchievementsDto?> GetGameAchievementsAsync(
        int gameId,
        string? language = null,
        CancellationToken cancellationToken = default);

    // ---- Creative content: artworks, tracks and stories (own tables and endpoints, not games) ----

    Task<IReadOnlyList<ArtworkDto>> GetArtworksAsync(CreativeQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Multipart POST api/art with fields title, description, tags (comma separated) and file.</summary>
    Task<(ArtworkDto? Item, IReadOnlyList<string> Errors)> UploadArtworkAsync(
        string title,
        string? description,
        IReadOnlyList<string> tags,
        string localFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TrackDto>> GetTracksAsync(CreativeQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Multipart POST api/music with fields title, description, tags, file and optional cover.</summary>
    Task<(TrackDto? Item, IReadOnlyList<string> Errors)> UploadTrackAsync(
        string title,
        string? description,
        IReadOnlyList<string> tags,
        string localFilePath,
        string? coverFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoryDto>> GetStoriesAsync(CreativeQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>The story with all its chapters.</summary>
    Task<StoryDto?> GetStoryAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>POST api/stories (JSON), then POST api/stories/{id}/cover when <paramref name="coverFilePath"/> is set.</summary>
    Task<(StoryDto? Item, IReadOnlyList<string> Errors)> CreateStoryAsync(
        CreateStoryRequestDto request,
        string? coverFilePath,
        CancellationToken cancellationToken = default);

    // ---- Creative content: own works, editing, admin and comments (section = "art" | "music" | "stories") ----

    Task<IReadOnlyList<ArtworkDto>> GetMyArtworksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackDto>> GetMyTracksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoryDto>> GetMyStoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin only: every item, including other authors'.</summary>
    Task<IReadOnlyList<ArtworkDto>> GetAdminArtworksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackDto>> GetAdminTracksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoryDto>> GetAdminStoriesAsync(CancellationToken cancellationToken = default);

    Task<(ArtworkDto? Item, IReadOnlyList<string> Errors)> UpdateArtworkAsync(
        int id, UpdateCreativeRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>PUT metadata, then POST api/music/{id}/cover when <paramref name="newCoverFilePath"/> is set.</summary>
    Task<(TrackDto? Item, IReadOnlyList<string> Errors)> UpdateTrackAsync(
        int id, UpdateCreativeRequestDto request, string? newCoverFilePath, CancellationToken cancellationToken = default);

    /// <summary>PUT story (title, description, tags, all chapters), then POST api/stories/{id}/cover when a new cover is given.</summary>
    Task<(StoryDto? Item, IReadOnlyList<string> Errors)> UpdateStoryAsync(
        int id, UpdateStoryRequestDto request, string? newCoverFilePath, CancellationToken cancellationToken = default);

    Task<bool> DeleteCreativeAsync(string section, int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CreativeCommentDto>> GetCreativeCommentsAsync(
        string section, int itemId, CancellationToken cancellationToken = default);

    Task<(CreativeCommentDto? Item, IReadOnlyList<string> Errors)> AddCreativeCommentAsync(
        string section, int itemId, string text, CancellationToken cancellationToken = default);

    Task<bool> DeleteCreativeCommentAsync(
        string section, int itemId, long commentId, CancellationToken cancellationToken = default);
}
