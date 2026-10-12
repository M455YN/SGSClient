using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

/// <summary>
/// Achievement definitions (creator side) and per-player state. Failures follow the other services:
/// <see cref="ArgumentException"/> = invalid input, <see cref="KeyNotFoundException"/> = unknown or
/// not-yours game/key, <see cref="UnauthorizedAccessException"/> = the player does not have the game.
/// The creator-side methods work for the game's owner, or for any game when <c>isAdmin</c> is true.
/// </summary>
public interface IAchievementService
{
    /// <summary>
    /// Null when the game is missing or a draft the viewer does not own. <paramref name="language"/>
    /// ("en", …) picks a translation; the base text is used when there is none.
    /// </summary>
    Task<GameAchievementsDto?> GetForGameAsync(
        int gameId,
        int? viewerUserId,
        string? language = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AchievementDefinitionDto>> GetDefinitionsAsync(
        int gameId,
        int ownerUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts by key and deactivates keys that are not in the request.</summary>
    Task<IReadOnlyList<AchievementDefinitionDto>> SyncDefinitionsAsync(
        int gameId,
        int ownerUserId,
        SyncGameAchievementsRequest request,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an uploaded image as the icon of an existing achievement.</summary>
    Task<AchievementDefinitionDto> SetIconAsync(
        int gameId,
        int ownerUserId,
        string key,
        Stream content,
        string fileName,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);

    Task<AchievementUpdateResultDto> UnlockAsync(
        int userId,
        int gameId,
        string key,
        CancellationToken cancellationToken = default);

    Task<AchievementUpdateResultDto> SetProgressAsync(
        int userId,
        int gameId,
        string key,
        int value,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserAchievementSummaryDto>> GetSummariesAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Clears the caller's own progress for the game, so a creator can retest.</summary>
    Task ResetProgressAsync(
        int gameId,
        int ownerUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);
}
