using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Packaging;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfEntitlementService(SgsServerDbContext db) : IEntitlementService
{
    public Task<bool> HasAsync(int userId, int gameId, CancellationToken cancellationToken = default) =>
        db.UserGameEntitlements.AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken);

    public async Task<IReadOnlyList<GameDto>> ListAsync(int userId, CancellationToken cancellationToken = default)
    {
        var gameIds = await db.UserGameEntitlements.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.GrantedAtUtc)
            .Select(e => e.GameId)
            .ToListAsync(cancellationToken);

        if (gameIds.Count == 0)
        {
            return [];
        }

        var games = await db.Games.AsNoTracking()
            .Where(g => gameIds.Contains(g.Id) && !g.DraftP)
            .Select(GameProjections.ToDto())
            .ToListAsync(cancellationToken);

        var order = gameIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return games.OrderBy(g => order.GetValueOrDefault(g.Id, int.MaxValue)).ToList();
    }

    public async Task<GameDto> ClaimFreeAsync(int userId, int gameId, CancellationToken cancellationToken = default)
    {
        var game = await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId && !g.DraftP)
            .Select(g => new { g.Id, g.Kind, g.ParentGameId })
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null)
        {
            throw new KeyNotFoundException("Gra nie istnieje lub nie jest opublikowana.");
        }

        if (game.Kind == GameContentKind.Dlc)
        {
            if (game.ParentGameId is not int parentId || parentId <= 0)
            {
                throw new InvalidOperationException("DLC nie ma powiązanej gry bazowej.");
            }

            var hasParent = await HasAsync(userId, parentId, cancellationToken);
            if (!hasParent)
            {
                throw new InvalidOperationException(
                    "Najpierw dodaj grę bazową do biblioteki, zanim dodasz DLC.");
            }
        }

        var existing = await db.UserGameEntitlements
            .FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken);

        if (existing is null)
        {
            db.UserGameEntitlements.Add(new UserGameEntitlementEntity
            {
                UserId = userId,
                GameId = gameId,
                GrantedAtUtc = DateTimeOffset.UtcNow,
                Source = EntitlementSources.FreeClaim
            });
        }

        var wishlist = await db.UserGameWishlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.GameId == gameId, cancellationToken);
        if (wishlist is not null)
        {
            db.UserGameWishlists.Remove(wishlist);
        }

        await db.SaveChangesAsync(cancellationToken);

        var dto = await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(GameProjections.ToDto())
            .FirstAsync(cancellationToken);
        return dto;
    }

    public async Task<bool> RevokeAsync(int userId, int gameId, CancellationToken cancellationToken = default)
    {
        var row = await db.UserGameEntitlements
            .FirstOrDefaultAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        db.UserGameEntitlements.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CanDownloadPackageAsync(
        int userId,
        string symbol,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (isAdmin)
        {
            return true;
        }

        var game = await db.Games.AsNoTracking()
            .Where(g => g.Symbol == symbol)
            .Select(g => new { g.Id, g.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null)
        {
            return false;
        }

        if (game.UserId == userId)
        {
            return true;
        }

        return await HasAsync(userId, game.Id, cancellationToken);
    }
}
