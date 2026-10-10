using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Packaging;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfWishlistService(SgsServerDbContext db) : IWishlistService
{
    public async Task<IReadOnlyList<GameDto>> ListAsync(int userId, CancellationToken cancellationToken = default)
    {
        var gameIds = await db.UserGameWishlists.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAtUtc)
            .Select(w => w.GameId)
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

    public async Task<GameDto?> AddAsync(int userId, int gameId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Games.AsNoTracking()
            .AnyAsync(g => g.Id == gameId && !g.DraftP, cancellationToken);
        if (!exists)
        {
            return null;
        }

        var row = await db.UserGameWishlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.GameId == gameId, cancellationToken);
        if (row is null)
        {
            db.UserGameWishlists.Add(new UserGameWishlistEntity
            {
                UserId = userId,
                GameId = gameId,
                AddedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(GameProjections.ToDto())
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(int userId, int gameId, CancellationToken cancellationToken = default)
    {
        var row = await db.UserGameWishlists
            .FirstOrDefaultAsync(w => w.UserId == userId && w.GameId == gameId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        db.UserGameWishlists.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
