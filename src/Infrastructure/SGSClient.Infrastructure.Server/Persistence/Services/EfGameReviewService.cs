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

    public async Task<GameReviewsSummaryDto> GetForGameAsync(
        int gameId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Games.AsNoTracking().AnyAsync(g => g.Id == gameId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException("Nie znaleziono gry.");
        }

        var rows = await db.GameReviews.AsNoTracking()
            .Include(r => r.Author)
            .Where(r => r.GameId == gameId)
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var recommended = rows.Count(r => r.Recommended);
        var total = rows.Count;
        GameReviewDto? mine = null;
        var canReview = false;

        if (viewerUserId is int vid)
        {
            canReview = await entitlements.HasAsync(vid, gameId, cancellationToken);
            mine = rows
                .Where(r => r.AuthorUserId == vid)
                .Select(r => Map(r, vid))
                .FirstOrDefault();
        }

        return new GameReviewsSummaryDto
        {
            TotalCount = total,
            RecommendedCount = recommended,
            NotRecommendedCount = total - recommended,
            RecommendedPercent = total == 0 ? 0 : Math.Round(100.0 * recommended / total, 1),
            MyReview = mine,
            CanReview = canReview,
            Reviews = rows.Select(r => Map(r, viewerUserId)).ToList()
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
        return Map(existing, authorUserId);
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

    private static GameReviewDto Map(GameReviewEntity r, int? viewerUserId) => new()
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
        IsMine = viewerUserId == r.AuthorUserId
    };
}
