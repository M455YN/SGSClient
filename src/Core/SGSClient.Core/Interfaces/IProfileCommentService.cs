using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IProfileCommentService
{
    Task<PublicUserProfileDto> GetPublicProfileAsync(
        int targetUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<PublicUserProfileDto?> GetPublicProfileByTagAsync(
        string playerTag,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<ProfileCommentDto> AddCommentAsync(
        int targetUserId,
        int authorUserId,
        string text,
        CancellationToken cancellationToken = default);

    Task DeleteCommentAsync(
        long commentId,
        int actorUserId,
        CancellationToken cancellationToken = default);
}
