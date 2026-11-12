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

    Task<IReadOnlyList<GameStatDefinitionDto>> GetStatDefinitionsAsync(
        int gameId,
        int ownerUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts by key and deactivates keys that are not in the request; the same for stats when
    /// <see cref="SyncGameAchievementsRequest.Stats"/> is set.
    /// </summary>
    Task<IReadOnlyList<AchievementDefinitionDto>> SyncDefinitionsAsync(
        int gameId,
        int ownerUserId,
        SyncGameAchievementsRequest request,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);

    /// <summary>Stores an uploaded image as the achieved (or, with <paramref name="locked"/>, unachieved) icon.</summary>
    Task<AchievementDefinitionDto> SetIconAsync(
        int gameId,
        int ownerUserId,
        string key,
        Stream content,
        string fileName,
        bool isAdmin = false,
        bool locked = false,
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

    /// <summary>
    /// Sets a stat (absolute value, checked against its type, range, max change and increment-only) and
    /// moves every achievement linked to it, unlocking those whose target the value reaches.
    /// </summary>
    Task<StatUpdateResultDto> SetStatAsync(
        int userId,
        int gameId,
        string key,
        double value,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Per game: unlocked of total. <paramref name="includeLocked"/> adds the library games with no unlock
    /// yet (the player's own view only).
    /// </summary>
    Task<IReadOnlyList<UserAchievementSummaryDto>> GetSummariesAsync(
        int userId,
        bool includeLocked = false,
        CancellationToken cancellationToken = default);

    /// <summary>Clears the caller's own achievements and stats for the game, so a creator can retest.</summary>
    Task ResetProgressAsync(
        int gameId,
        int ownerUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default);
}
