using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class PlaytimeService(SgsServerDbContext db) : IPlaytimeService
{
    public const long MaxSessionSeconds = 24 * 60 * 60;

    public async Task<IReadOnlyList<PlaytimeDto>> GetAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await db.UserGamePlaytimes.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LastPlayedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<PlaytimeDto?> GetAsync(int userId, int gameId, CancellationToken cancellationToken = default)
    {
        var row = await db.UserGamePlaytimes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GameId == gameId, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<PlaytimeDto> RecordSessionAsync(
        int userId,
        RecordPlaytimeSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.GameId <= 0)
        {
            throw new ArgumentException("Nieprawidłowa gra.");
        }

        var seconds = Math.Clamp(request.Seconds, 1, MaxSessionSeconds);
        var endedAt = request.EndedAtUtc == default
            ? DateTimeOffset.UtcNow
            : request.EndedAtUtc.ToUniversalTime();

        var gameExists = await db.Games.AnyAsync(g => g.Id == request.GameId, cancellationToken);
        if (!gameExists)
        {
            throw new KeyNotFoundException("Gra nie istnieje.");
        }

        var row = await db.UserGamePlaytimes
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GameId == request.GameId, cancellationToken);

        if (row is null)
        {
            row = new UserGamePlaytimeEntity
            {
                UserId = userId,
                GameId = request.GameId,
                TotalSeconds = seconds,
                LastPlayedUtc = endedAt,
                SessionCount = 1
            };
            db.UserGamePlaytimes.Add(row);
        }
        else
        {
            row.TotalSeconds += seconds;
            row.SessionCount += 1;
            if (row.LastPlayedUtc is null || endedAt > row.LastPlayedUtc)
            {
                row.LastPlayedUtc = endedAt;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Map(row);
    }

    private static PlaytimeDto Map(UserGamePlaytimeEntity row) => new()
    {
        GameId = row.GameId,
        TotalSeconds = row.TotalSeconds,
        LastPlayedUtc = row.LastPlayedUtc,
        SessionCount = row.SessionCount
    };
}
