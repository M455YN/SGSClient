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
}
