using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IGameReviewService
{
    Task<GameReviewsSummaryDto> GetForGameAsync(
        int gameId,
        int? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<GameReviewDto> UpsertAsync(
        int gameId,
        int authorUserId,
        UpsertGameReviewRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int gameId, int authorUserId, CancellationToken cancellationToken = default);
}
