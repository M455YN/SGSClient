using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfGameReviewService(
    SgsServerDbContext db,
    IEntitlementService entitlements) : IGameReviewService
{
    private const int MaxReviewLength = 4000;
    private const int MaxCommentLength = 1000;

    public async Task<GameReviewsSummaryDto> GetForGameAsync(
        int gameId,
        int? viewerUserId,
        string? sort = null,
        bool? recommended = null,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Games.AsNoTracking().AnyAsync(g => g.Id == gameId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("Nie znaleziono gry.");
        }

        var query = db.GameReviews.AsNoTracking()
            .Include(r => r.Author)
            .Where(r => r.GameId == gameId);

        if (recommended is bool rec)
        {
            query = query.Where(r => r.Recommended == rec);
        }

        var rows = await query.ToListAsync(cancellationToken);

        var reviewIds = rows.Select(r => r.Id).ToList();
        var helpfulCounts = await db.GameReviewVotes.AsNoTracking()
            .Where(v => reviewIds.Contains(v.ReviewId) && v.IsHelpful)
            .GroupBy(v => v.ReviewId)
            .Select(g => new { ReviewId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ReviewId, x => x.Count, cancellationToken);

        var commentCounts = await db.GameReviewComments.AsNoTracking()
            .Where(c => reviewIds.Contains(c.ReviewId))
            .GroupBy(c => c.ReviewId)
            .Select(g => new { ReviewId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ReviewId, x => x.Count, cancellationToken);

        Dictionary<long, bool> myVotes = [];
        if (viewerUserId is int vid)
        {
            myVotes = await db.GameReviewVotes.AsNoTracking()
                .Where(v => v.UserId == vid && reviewIds.Contains(v.ReviewId))
                .ToDictionaryAsync(v => v.ReviewId, v => v.IsHelpful, cancellationToken);
        }

        var sortKey = (sort ?? "recent").Trim().ToLowerInvariant();
        rows = sortKey switch
        {
            "helpful" => rows
                .OrderByDescending(r => helpfulCounts.GetValueOrDefault(r.Id))
                .ThenByDescending(r => r.UpdatedAtUtc)
                .Take(100)
                .ToList(),
            _ => rows
                .OrderByDescending(r => r.UpdatedAtUtc)
                .Take(100)
                .ToList()
        };

        var allForStats = await db.GameReviews.AsNoTracking()
            .Where(r => r.GameId == gameId)
            .Select(r => r.Recommended)
            .ToListAsync(cancellationToken);
        var recommendedCount = allForStats.Count(r => r);
        var total = allForStats.Count;

        GameReviewDto? mine = null;
        var canReview = false;
        if (viewerUserId is int viewer)
        {
            canReview = await entitlements.HasAsync(viewer, gameId, cancellationToken);
            var myRow = await db.GameReviews.AsNoTracking()
                .Include(r => r.Author)
                .FirstOrDefaultAsync(r => r.GameId == gameId && r.AuthorUserId == viewer, cancellationToken);
            if (myRow is not null)
            {
                mine = Map(
                    myRow,
                    viewer,
                    helpfulCounts.GetValueOrDefault(myRow.Id),
                    myVotes.TryGetValue(myRow.Id, out var mv) ? mv : null,
                    commentCounts.GetValueOrDefault(myRow.Id));
            }
        }

        return new GameReviewsSummaryDto
        {
            TotalCount = total,
            RecommendedCount = recommendedCount,
            NotRecommendedCount = total - recommendedCount,
            RecommendedPercent = total == 0 ? 0 : Math.Round(100.0 * recommendedCount / total, 1),
            MyReview = mine,
            CanReview = canReview,
            Reviews = rows.Select(r => Map(
                    r,
                    viewerUserId,
                    helpfulCounts.GetValueOrDefault(r.Id),
                    myVotes.TryGetValue(r.Id, out var vote) ? vote : null,
                    commentCounts.GetValueOrDefault(r.Id)))
                .ToList()
        };
    }

    public async Task<GameReviewDto> UpsertAsync(
        int gameId,
        int authorUserId,
        UpsertGameReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length is < 10 or > MaxReviewLength)
        {
            throw new ArgumentException($"Recenzja musi mieć od 10 do {MaxReviewLength} znaków.");
        }

        if (!await entitlements.HasAsync(authorUserId, gameId, cancellationToken))
        {
            throw new UnauthorizedAccessException("Recenzję mogą dodać tylko właściciele gry.");
        }

        if (!await db.Games.AnyAsync(g => g.Id == gameId, cancellationToken))
        {
            throw new KeyNotFoundException("Nie znaleziono gry.");
        }

        long? playtime = null;
        var play = await db.UserGamePlaytimes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == authorUserId && p.GameId == gameId, cancellationToken);
        if (play is not null)
        {
            playtime = play.TotalSeconds;
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await db.GameReviews
            .FirstOrDefaultAsync(r => r.GameId == gameId && r.AuthorUserId == authorUserId, cancellationToken);

        if (existing is null)
        {
            existing = new GameReviewEntity
            {
                GameId = gameId,
                AuthorUserId = authorUserId,
                Recommended = request.Recommended,
                Text = text,
                PlaytimeSecondsAtReview = playtime,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.GameReviews.Add(existing);
        }
        else
        {
            existing.Recommended = request.Recommended;
            existing.Text = text;
            existing.PlaytimeSecondsAtReview = playtime;
            existing.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);

        var author = await db.Users.AsNoTracking().FirstAsync(u => u.Id == authorUserId, cancellationToken);
        existing.Author = author;
        return Map(existing, authorUserId, 0, null, 0);
    }

    public async Task DeleteAsync(int gameId, int authorUserId, CancellationToken cancellationToken = default)
    {
        var existing = await db.GameReviews
            .FirstOrDefaultAsync(r => r.GameId == gameId && r.AuthorUserId == authorUserId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.GameReviews.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task VoteAsync(
        long reviewId,
        int userId,
        bool helpful,
        CancellationToken cancellationToken = default)
    {
        var review = await db.GameReviews.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono recenzji.");

        if (review.AuthorUserId == userId)
        {
            throw new InvalidOperationException("Nie możesz głosować na własną recenzję.");
        }

        var existing = await db.GameReviewVotes
            .FirstOrDefaultAsync(v => v.ReviewId == reviewId && v.UserId == userId, cancellationToken);
        if (existing is null)
        {
            db.GameReviewVotes.Add(new GameReviewVoteEntity
            {
                ReviewId = reviewId,
                UserId = userId,
                IsHelpful = helpful,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else if (existing.IsHelpful == helpful)
        {
            db.GameReviewVotes.Remove(existing);
        }
        else
        {
            existing.IsHelpful = helpful;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveVoteAsync(long reviewId, int userId, CancellationToken cancellationToken = default)
    {
        var existing = await db.GameReviewVotes
            .FirstOrDefaultAsync(v => v.ReviewId == reviewId && v.UserId == userId, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.GameReviewVotes.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameReviewCommentDto>> GetCommentsAsync(
        long reviewId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await db.GameReviews.AsNoTracking().AnyAsync(r => r.Id == reviewId, cancellationToken))
        {
            throw new KeyNotFoundException("Nie znaleziono recenzji.");
        }

        var comments = await db.GameReviewComments.AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.ReviewId == reviewId)
            .OrderBy(c => c.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return comments.Select(c => MapComment(c, viewerUserId)).ToList();
    }

    public async Task<GameReviewCommentDto> AddCommentAsync(
        long reviewId,
        int authorUserId,
        CreateGameReviewCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = (request.Text ?? string.Empty).Trim();
        if (text.Length is < 1 or > MaxCommentLength)
        {
            throw new ArgumentException($"Komentarz musi mieć od 1 do {MaxCommentLength} znaków.");
        }

        if (!await db.GameReviews.AsNoTracking().AnyAsync(r => r.Id == reviewId, cancellationToken))
        {
            throw new KeyNotFoundException("Nie znaleziono recenzji.");
        }

        var entity = new GameReviewCommentEntity
        {
            ReviewId = reviewId,
            AuthorUserId = authorUserId,
            Text = text,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.GameReviewComments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var author = await db.Users.AsNoTracking().FirstAsync(u => u.Id == authorUserId, cancellationToken);
        entity.Author = author;
        return MapComment(entity, authorUserId);
    }

    public async Task DeleteCommentAsync(long commentId, int authorUserId, CancellationToken cancellationToken = default)
    {
        var comment = await db.GameReviewComments
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return;
        }

        if (comment.AuthorUserId != authorUserId)
        {
            throw new UnauthorizedAccessException("Możesz usunąć tylko własny komentarz.");
        }

        db.GameReviewComments.Remove(comment);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static GameReviewDto Map(
        GameReviewEntity r,
        int? viewerUserId,
        int helpfulCount,
        bool? myVote,
        int commentCount) => new()
    {
        Id = r.Id,
        GameId = r.GameId,
        AuthorUserId = r.AuthorUserId,
        AuthorDisplayName = r.Author?.DisplayName ?? "Gracz",
        AuthorAvatarUrl = r.Author?.AvatarUrl,
        AuthorPlayerTag = r.Author?.PlayerTag,
        Recommended = r.Recommended,
        Text = r.Text,
        PlaytimeSecondsAtReview = r.PlaytimeSecondsAtReview,
        CreatedAtUtc = r.CreatedAtUtc,
        UpdatedAtUtc = r.UpdatedAtUtc,
        IsMine = viewerUserId == r.AuthorUserId,
        HelpfulCount = helpfulCount,
        MyHelpfulVote = myVote,
        CommentCount = commentCount
    };

    private static GameReviewCommentDto MapComment(GameReviewCommentEntity c, int? viewerUserId) => new()
    {
        Id = c.Id,
        ReviewId = c.ReviewId,
        AuthorUserId = c.AuthorUserId,
        AuthorDisplayName = c.Author?.DisplayName ?? "Gracz",
        AuthorPlayerTag = c.Author?.PlayerTag,
        Text = c.Text,
        CreatedAtUtc = c.CreatedAtUtc,
        IsMine = viewerUserId == c.AuthorUserId
    };
}
