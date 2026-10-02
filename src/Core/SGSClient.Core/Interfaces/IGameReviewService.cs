using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IGameReviewService
{
    Task<GameReviewsSummaryDto> GetForGameAsync(
        int gameId,
        int? viewerUserId,
        string? sort = null,
        bool? recommended = null,
        CancellationToken cancellationToken = default);

    Task<GameReviewDto> UpsertAsync(
        int gameId,
        int authorUserId,
        UpsertGameReviewRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int gameId, int authorUserId, CancellationToken cancellationToken = default);

    Task VoteAsync(
        long reviewId,
        int userId,
        bool helpful,
        CancellationToken cancellationToken = default);

    Task RemoveVoteAsync(long reviewId, int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameReviewCommentDto>> GetCommentsAsync(
        long reviewId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<GameReviewCommentDto> AddCommentAsync(
        long reviewId,
        int authorUserId,
        CreateGameReviewCommentRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteCommentAsync(long commentId, int authorUserId, CancellationToken cancellationToken = default);
}
