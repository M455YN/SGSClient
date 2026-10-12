using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IGameMediaService
{
    /// <param name="requireOwnerUserId">When set, only that owner may mutate; null = admin/any.</param>
    Task<GameDto?> SetCoverAsync(
        int gameId,
        Stream content,
        string fileName,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> ClearCoverAsync(
        int gameId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> SetHeaderAsync(
        int gameId,
        Stream content,
        string fileName,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> ClearHeaderAsync(
        int gameId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> SetLogoAsync(
        int gameId,
        Stream content,
        string fileName,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> ClearLogoAsync(
        int gameId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> SetIntroAsync(
        int gameId,
        Stream content,
        string fileName,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> ClearIntroAsync(
        int gameId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> AddScreenshotAsync(
        int gameId,
        Stream content,
        string fileName,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<GameDto?> RemoveScreenshotAsync(
        int gameId,
        int screenshotId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an achievement icon next to the game's media and returns its public URL (null when the
    /// game is missing). The caller has already authorized the write; the previous file is removed.
    /// </summary>
    Task<string?> SaveAchievementIconAsync(
        int gameId,
        string key,
        string? previousUrl,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);
}
