using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Persistence.Entities;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class LocalPlaytimeStore(
    SgsClientDbContext db,
    IAuthSession? auth = null,
    IUserApiClient? api = null) : IPlaytimeStore
{
    public const long MaxSessionSeconds = 24 * 60 * 60;

    public async Task<PlaytimeRecord?> GetAsync(int gameId, CancellationToken cancellationToken = default)
    {
        var userId = ResolveUserId();
        var row = await db.Playtimes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GameId == gameId, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyList<PlaytimeRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var userId = ResolveUserId();
        var rows = await db.Playtimes.AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task RecordSessionAsync(
        int gameId,
        TimeSpan duration,
        DateTimeOffset endedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var seconds = (long)Math.Round(duration.TotalSeconds);
        if (seconds < 1)
        {
            return;
        }

        seconds = Math.Clamp(seconds, 1, MaxSessionSeconds);
        var endedAt = endedAtUtc == default ? DateTimeOffset.UtcNow : endedAtUtc.ToUniversalTime();
        var userId = ResolveUserId();

        var row = await db.Playtimes
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GameId == gameId, cancellationToken);

        if (row is null)
        {
            row = new LocalPlaytimeEntity
            {
                UserId = userId,
                GameId = gameId,
                TotalSeconds = seconds,
                UnsyncedSeconds = seconds,
                LastPlayedUtc = endedAt,
                SessionCount = 1
            };
            db.Playtimes.Add(row);
        }
        else
        {
            row.TotalSeconds += seconds;
            row.UnsyncedSeconds += seconds;
            row.SessionCount += 1;
            if (row.LastPlayedUtc is null || endedAt > row.LastPlayedUtc)
            {
                row.LastPlayedUtc = endedAt;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await FlushUnsyncedAsync(row, cancellationToken);
    }

    public async Task SyncFromServerAsync(CancellationToken cancellationToken = default)
    {
        if (api is not null)
        {
            try
            {
                var remote = await api.GetPlaytimeAsync(cancellationToken);
                var userId = ResolveUserId();
                foreach (var dto in remote)
                {
                    await MergeServerRowAsync(userId, dto, cancellationToken);
                }

                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // stay on local totals when offline
            }
        }

        var pending = await db.Playtimes
            .Where(x => x.UserId == ResolveUserId() && x.UnsyncedSeconds > 0)
            .ToListAsync(cancellationToken);

        foreach (var row in pending)
        {
            await FlushUnsyncedAsync(row, cancellationToken);
        }
    }

    public async Task ApplyToLibraryAsync(
        IReadOnlyList<GameLibraryItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        var map = (await GetAllAsync(cancellationToken)).ToDictionary(x => x.GameId);
        foreach (var item in items)
        {
            if (!map.TryGetValue(item.GameId, out var record))
            {
                continue;
            }

            item.TotalPlayTimeSeconds = record.TotalSeconds;
            if (record.LastPlayedUtc is not null)
            {
                item.LastPlayedUtc = record.LastPlayedUtc;
            }
        }
    }

    private async Task MergeServerRowAsync(int userId, PlaytimeDto dto, CancellationToken cancellationToken)
    {
        var row = await db.Playtimes
            .FirstOrDefaultAsync(x => x.UserId == userId && x.GameId == dto.GameId, cancellationToken);

        if (row is null)
        {
            db.Playtimes.Add(new LocalPlaytimeEntity
            {
                UserId = userId,
                GameId = dto.GameId,
                TotalSeconds = dto.TotalSeconds,
                UnsyncedSeconds = 0,
                LastPlayedUtc = dto.LastPlayedUtc,
                SessionCount = dto.SessionCount
            });
            return;
        }

        if (dto.TotalSeconds > row.TotalSeconds)
        {
            row.TotalSeconds = dto.TotalSeconds;
        }

        if (dto.SessionCount > row.SessionCount)
        {
            row.SessionCount = dto.SessionCount;
        }

        if (dto.LastPlayedUtc is not null
            && (row.LastPlayedUtc is null || dto.LastPlayedUtc > row.LastPlayedUtc))
        {
            row.LastPlayedUtc = dto.LastPlayedUtc;
        }
    }

    private async Task FlushUnsyncedAsync(LocalPlaytimeEntity row, CancellationToken cancellationToken)
    {
        if (api is null || row.UnsyncedSeconds <= 0)
        {
            return;
        }

        try
        {
            var result = await api.RecordPlaytimeSessionAsync(
                new RecordPlaytimeSessionRequest
                {
                    GameId = row.GameId,
                    Seconds = row.UnsyncedSeconds,
                    EndedAtUtc = row.LastPlayedUtc ?? DateTimeOffset.UtcNow
                },
                cancellationToken);

            if (result is null)
            {
                return;
            }

            row.UnsyncedSeconds = 0;
            if (result.TotalSeconds > row.TotalSeconds)
            {
                row.TotalSeconds = result.TotalSeconds;
            }

            if (result.SessionCount > row.SessionCount)
            {
                row.SessionCount = result.SessionCount;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // keep UnsyncedSeconds for a later flush
        }
    }

    private int ResolveUserId()
    {
        if (auth?.CurrentUser?.Id > 0)
        {
            return auth.CurrentUser.Id;
        }

        // Local-dev (Azure AD off) uses the same owner id as the API fallback.
        if (auth is null || !auth.IsAvailable)
        {
            return 1;
        }

        return 0;
    }

    private static PlaytimeRecord Map(LocalPlaytimeEntity row) => new()
    {
        GameId = row.GameId,
        TotalSeconds = row.TotalSeconds,
        LastPlayedUtc = row.LastPlayedUtc,
        SessionCount = row.SessionCount
    };
}
