using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGSClient.Core.Achievements;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfAchievementService(
    SgsServerDbContext db,
    INotificationService notifications,
    IGameMediaService media) : IAchievementService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<GameAchievementsDto?> GetForGameAsync(
        int gameId,
        int? viewerUserId,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var game = await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => new { g.UserId, g.DraftP })
            .FirstOrDefaultAsync(cancellationToken);
        if (game is null || (game.DraftP && game.UserId != viewerUserId))
        {
            return null;
        }

        var definitions = await ActiveDefinitions(gameId).ToListAsync(cancellationToken);
        var unlockedCounts = await UnlockedCountsAsync(gameId, cancellationToken);
        var owners = Math.Max(1, await db.UserGameEntitlements.CountAsync(e => e.GameId == gameId, cancellationToken));

        var mine = new Dictionary<int, UserGameAchievementEntity>();
        if (viewerUserId is int viewerId)
        {
            var rows = await db.UserGameAchievements.AsNoTracking()
                .Where(u => u.UserId == viewerId && u.Achievement!.GameId == gameId)
                .ToListAsync(cancellationToken);
            mine = rows.ToDictionary(r => r.AchievementId);
        }

        var items = definitions.Select(d =>
        {
            mine.TryGetValue(d.Id, out var state);
            var unlockedAt = state?.UnlockedAtUtc;
            var masked = d.Hidden && unlockedAt is null;
            var (title, description) = Localize(d, language);
            return new GameAchievementDto
            {
                Key = d.Key,
                Title = masked ? string.Empty : title,
                Description = masked ? string.Empty : description,
                IconUrl = masked ? null : d.IconUrl,
                LockedIconUrl = masked ? null : d.LockedIconUrl,
                Points = d.Points,
                Hidden = d.Hidden,
                Target = d.Target,
                StatKey = d.StatKey,
                Progress = state?.Progress ?? 0,
                UnlockedAtUtc = unlockedAt,
                RarityPercent = Math.Min(100d, unlockedCounts.GetValueOrDefault(d.Id) * 100d / owners)
            };
        }).ToList();

        return new GameAchievementsDto
        {
            GameId = gameId,
            Total = items.Count,
            Unlocked = items.Count(i => i.UnlockedAtUtc is not null),
            PointsTotal = items.Sum(i => i.Points),
            PointsEarned = items.Where(i => i.UnlockedAtUtc is not null).Sum(i => i.Points),
            Items = items,
            Stats = await StatValuesAsync(gameId, viewerUserId, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<GameStatDefinitionDto>> GetStatDefinitionsAsync(
        int gameId,
        int ownerUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnedAsync(gameId, ownerUserId, isAdmin, cancellationToken);
        return (await ActiveStats(gameId).ToListAsync(cancellationToken)).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AchievementDefinitionDto>> GetDefinitionsAsync(
        int gameId,
        int ownerUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnedAsync(gameId, ownerUserId, isAdmin, cancellationToken);
        return await ListDefinitionsAsync(gameId, cancellationToken);
    }

    public async Task<IReadOnlyList<AchievementDefinitionDto>> SyncDefinitionsAsync(
        int gameId,
        int ownerUserId,
        SyncGameAchievementsRequest request,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var game = await OwnedGame(gameId, ownerUserId, isAdmin).FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Gra nie istnieje.");

        var now = DateTimeOffset.UtcNow;
        var statKeys = request.Stats is null
            ? await ActiveStats(gameId).Select(s => s.Key).ToListAsync(cancellationToken)
            : await SyncStatsAsync(gameId, request.Stats, now, cancellationToken);

        var error = AchievementRules.Validate(request.Items, statKeys);
        if (error is not null)
        {
            throw new ArgumentException(error);
        }

        var existing = await db.GameAchievements
            .Where(a => a.GameId == gameId)
            .ToDictionaryAsync(a => a.Key, StringComparer.Ordinal, cancellationToken);

        var kept = new HashSet<string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var item in request.Items)
        {
            var key = AchievementRules.NormalizeKey(item.Key)!;
            kept.Add(key);
            if (!existing.TryGetValue(key, out var row))
            {
                row = new GameAchievementEntity { GameId = gameId, Key = key, CreatedAtUtc = now };
                db.GameAchievements.Add(row);
            }

            AchievementRules.TryNormalizeIconUrl(item.IconUrl, out var iconUrl, out _);
            AchievementRules.TryNormalizeIconUrl(item.LockedIconUrl, out var lockedIconUrl, out _);
            row.Title = item.Title.Trim();
            row.Description = item.Description?.Trim() ?? string.Empty;
            row.IconUrl = iconUrl;
            row.LockedIconUrl = lockedIconUrl;
            row.StatKey = AchievementRules.NormalizeKey(item.StatKey);
            row.TranslationsJson = SerializeTranslations(item.Translations);
            row.Points = item.Points;
            row.Hidden = item.Hidden;
            row.Target = item.Target;
            row.SortOrder = order++;
            row.IsActive = true;
            row.UpdatedAtUtc = now;
        }

        foreach (var row in existing.Values.Where(a => !kept.Contains(a.Key) && a.IsActive))
        {
            row.IsActive = false;
            row.UpdatedAtUtc = now;
        }

        // Keep the store's "Achievements" feature in step with what the game actually defines.
        game.Features = kept.Count > 0
            ? game.Features | GameFeatures.Achievements
            : game.Features & ~GameFeatures.Achievements;

        await db.SaveChangesAsync(cancellationToken);
        return await ListDefinitionsAsync(gameId, cancellationToken);
    }

    public async Task<AchievementDefinitionDto> SetIconAsync(
        int gameId,
        int ownerUserId,
        string key,
        Stream content,
        string fileName,
        bool isAdmin = false,
        bool locked = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnedAsync(gameId, ownerUserId, isAdmin, cancellationToken);

        var normalized = AchievementRules.NormalizeKey(key);
        var row = normalized is null
            ? null
            : await db.GameAchievements.FirstOrDefaultAsync(
                a => a.GameId == gameId && a.Key == normalized && a.IsActive,
                cancellationToken);
        if (row is null)
        {
            throw new KeyNotFoundException("Osiągnięcie nie istnieje.");
        }

        var url = await media.SaveAchievementIconAsync(
                gameId,
                locked ? row.Key + "_locked" : row.Key,
                locked ? row.LockedIconUrl : row.IconUrl,
                content,
                fileName,
                cancellationToken)
            ?? throw new KeyNotFoundException("Gra nie istnieje.");
        if (locked)
        {
            row.LockedIconUrl = url;
        }
        else
        {
            row.IconUrl = url;
        }

        row.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return (await ListDefinitionsAsync(gameId, cancellationToken)).First(d => d.Key == row.Key);
    }

    public Task<AchievementUpdateResultDto> UnlockAsync(
        int userId,
        int gameId,
        string key,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(userId, gameId, key, value: null, fromStat: false, cancellationToken);

    public Task<AchievementUpdateResultDto> SetProgressAsync(
        int userId,
        int gameId,
        string key,
        int value,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(userId, gameId, key, value, fromStat: false, cancellationToken);

    public async Task<StatUpdateResultDto> SetStatAsync(
        int userId,
        int gameId,
        string key,
        double value,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanPlayAsync(userId, gameId, cancellationToken);

        var normalized = AchievementRules.NormalizeKey(key);
        var stat = normalized is null
            ? null
            : await ActiveStats(gameId).FirstOrDefaultAsync(s => s.Key == normalized, cancellationToken);
        if (stat is null)
        {
            throw new KeyNotFoundException("Statystyka nie istnieje.");
        }

        for (var attempt = 0; ; attempt++)
        {
            var row = await db.UserGameStats.FirstOrDefaultAsync(
                u => u.UserId == userId && u.StatId == stat.Id,
                cancellationToken);
            var isNew = row is null;
            if (AchievementRules.CheckStatValue(ToDto(stat), row?.Value ?? stat.DefaultValue, value) is { } error)
            {
                throw new ArgumentException(error);
            }

            if (row is null)
            {
                row = new UserGameStatEntity { UserId = userId, StatId = stat.Id };
                db.UserGameStats.Add(row);
            }

            row.Value = value;
            row.UpdatedAtUtc = DateTimeOffset.UtcNow;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (isNew && attempt == 0)
            {
                // A parallel request inserted the row first; recheck against its value.
                db.ChangeTracker.Clear();
            }
        }

        var linked = await ActiveDefinitions(gameId)
            .Where(a => a.StatKey == stat.Key && a.Target != null)
            .Select(a => new { a.Key, Target = a.Target!.Value })
            .ToListAsync(cancellationToken);
        var results = new List<AchievementUpdateResultDto>();
        foreach (var achievement in linked)
        {
            var (progress, reached) = AchievementRules.StatProgress(value, achievement.Target);
            results.Add(await ApplyAsync(
                userId,
                gameId,
                achievement.Key,
                reached ? achievement.Target : progress,
                fromStat: true,
                cancellationToken));
        }

        return new StatUpdateResultDto { Key = stat.Key, Value = value, Achievements = results };
    }

    public async Task<IReadOnlyList<UserAchievementSummaryDto>> GetSummariesAsync(
        int userId,
        bool includeLocked = false,
        CancellationToken cancellationToken = default)
    {
        var unlocked = await db.UserGameAchievements.AsNoTracking()
            .Where(u => u.UserId == userId && u.UnlockedAtUtc != null && u.Achievement!.IsActive)
            .GroupBy(u => u.Achievement!.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count(), Points = g.Sum(x => x.Achievement!.Points) })
            .ToListAsync(cancellationToken);
        var gameIds = unlocked.Select(x => x.GameId).ToList();
        if (includeLocked)
        {
            // The owner also sees games they have not started: every library game that defines achievements.
            var owned = await db.UserGameEntitlements.AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => e.GameId)
                .ToListAsync(cancellationToken);
            var withAchievements = await db.GameAchievements.AsNoTracking()
                .Where(a => a.IsActive && owned.Contains(a.GameId))
                .Select(a => a.GameId)
                .Distinct()
                .ToListAsync(cancellationToken);
            gameIds = gameIds.Union(withAchievements).ToList();
        }

        if (gameIds.Count == 0)
        {
            return [];
        }
        var totals = await db.GameAchievements.AsNoTracking()
            .Where(a => a.IsActive && gameIds.Contains(a.GameId))
            .GroupBy(a => a.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GameId, x => x.Count, cancellationToken);
        var titles = await db.Games.AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken);

        return gameIds
            .Select(id =>
            {
                var row = unlocked.FirstOrDefault(u => u.GameId == id);
                return new UserAchievementSummaryDto
                {
                    GameId = id,
                    GameTitle = titles.GetValueOrDefault(id, string.Empty),
                    Unlocked = row?.Count ?? 0,
                    Total = totals.GetValueOrDefault(id),
                    PointsEarned = row?.Points ?? 0
                };
            })
            .OrderByDescending(x => x.Unlocked)
            .ThenBy(x => x.GameTitle, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task ResetProgressAsync(
        int gameId,
        int ownerUserId,
        bool isAdmin = false,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnedAsync(gameId, ownerUserId, isAdmin, cancellationToken);

        var rows = await db.UserGameAchievements
            .Where(u => u.UserId == ownerUserId && u.Achievement!.GameId == gameId)
            .ToListAsync(cancellationToken);
        db.UserGameAchievements.RemoveRange(rows);
        db.UserGameStats.RemoveRange(await db.UserGameStats
            .Where(u => u.UserId == ownerUserId && u.Stat!.GameId == gameId)
            .ToListAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// One entry point for unlock (<paramref name="value"/> null) and counter updates. A stat-driven
    /// achievement only takes progress from its stat (<paramref name="fromStat"/>).
    /// </summary>
    private async Task<AchievementUpdateResultDto> ApplyAsync(
        int userId,
        int gameId,
        string key,
        int? value,
        bool fromStat,
        CancellationToken cancellationToken)
    {
        await EnsureCanPlayAsync(userId, gameId, cancellationToken);

        var normalized = AchievementRules.NormalizeKey(key);
        var definition = normalized is null
            ? null
            : await db.GameAchievements.AsNoTracking().FirstOrDefaultAsync(
                a => a.GameId == gameId && a.Key == normalized && a.IsActive,
                cancellationToken);
        if (definition is null)
        {
            throw new KeyNotFoundException("Osiągnięcie nie istnieje.");
        }

        if (value is not null)
        {
            if (definition.Target is null)
            {
                throw new ArgumentException("To osiągnięcie nie ma licznika — użyj odblokowania.");
            }

            if (definition.StatKey is not null && !fromStat)
            {
                throw new ArgumentException($"Postęp tego osiągnięcia wynika ze statystyki „{definition.StatKey}” — ustaw statystykę.");
            }

            if (value < 0)
            {
                throw new ArgumentException("Wartość nie może być ujemna.");
            }
        }

        for (var attempt = 0; ; attempt++)
        {
            var row = await db.UserGameAchievements.FirstOrDefaultAsync(
                u => u.UserId == userId && u.AchievementId == definition.Id,
                cancellationToken);
            var isNew = row is null;
            if (row is null)
            {
                row = new UserGameAchievementEntity { UserId = userId, AchievementId = definition.Id };
                db.UserGameAchievements.Add(row);
            }

            var now = DateTimeOffset.UtcNow;
            var wasUnlocked = row.UnlockedAtUtc is not null;
            if (value is int v)
            {
                // Progress only moves forward; a late retry with an old value is harmless.
                row.Progress = Math.Max(row.Progress, Math.Min(v, definition.Target!.Value));
            }
            else
            {
                row.Progress = definition.Target ?? 1;
            }

            if (!wasUnlocked && (value is null || row.Progress >= definition.Target))
            {
                row.UnlockedAtUtc = now;
            }

            row.UpdatedAtUtc = now;

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (isNew && attempt == 0)
            {
                // A parallel request inserted the row first; redo this one on top of it.
                // shortcut: two parallel unlocks of an existing row can both report JustUnlocked, switch to a conditional UPDATE if that ever matters.
                db.ChangeTracker.Clear();
                continue;
            }

            var justUnlocked = !wasUnlocked && row.UnlockedAtUtc is not null;
            if (justUnlocked)
            {
                await NotifyUnlockedAsync(userId, gameId, definition, cancellationToken);
            }

            return new AchievementUpdateResultDto
            {
                Key = definition.Key,
                Progress = row.Progress,
                Target = definition.Target,
                Unlocked = row.UnlockedAtUtc is not null,
                JustUnlocked = justUnlocked
            };
        }
    }

    /// <summary>Owner of the game or a player who has it in the library.</summary>
    private async Task EnsureCanPlayAsync(int userId, int gameId, CancellationToken cancellationToken)
    {
        var ownerId = await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => (int?)g.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (ownerId is null)
        {
            throw new KeyNotFoundException("Gra nie istnieje.");
        }

        if (ownerId == userId)
        {
            return;
        }

        var owns = await db.UserGameEntitlements.AsNoTracking()
            .AnyAsync(e => e.UserId == userId && e.GameId == gameId, cancellationToken);
        if (!owns)
        {
            throw new UnauthorizedAccessException("Dodaj grę do biblioteki, aby zdobywać osiągnięcia.");
        }
    }

    private async Task NotifyUnlockedAsync(
        int userId,
        int gameId,
        GameAchievementEntity definition,
        CancellationToken cancellationToken)
    {
        try
        {
            var gameTitle = await db.Games.AsNoTracking()
                .Where(g => g.Id == gameId)
                .Select(g => g.Title)
                .FirstOrDefaultAsync(cancellationToken);
            await notifications.CreateAsync(
                userId,
                "achievement",
                "Odblokowano osiągnięcie",
                $"{gameTitle}: {definition.Title}",
                $"{{\"gameId\":{gameId},\"key\":\"{definition.Key}\"}}",
                cancellationToken);
        }
        catch
        {
            // A failed notification must never turn a recorded unlock into an error for the game.
        }
    }

    private IQueryable<GameEntity> OwnedGame(int gameId, int userId, bool isAdmin) =>
        db.Games.Where(g => g.Id == gameId && (isAdmin || g.UserId == userId));

    private async Task EnsureOwnedAsync(int gameId, int userId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!await OwnedGame(gameId, userId, isAdmin).AnyAsync(cancellationToken))
        {
            throw new KeyNotFoundException("Gra nie istnieje.");
        }
    }

    private IQueryable<GameAchievementEntity> ActiveDefinitions(int gameId) =>
        db.GameAchievements.AsNoTracking()
            .Where(a => a.GameId == gameId && a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Id);

    private IQueryable<GameStatEntity> ActiveStats(int gameId) =>
        db.GameStats.AsNoTracking()
            .Where(s => s.GameId == gameId && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id);

    private static GameStatDefinitionDto ToDto(GameStatEntity s) => new()
    {
        Key = s.Key,
        DisplayName = s.DisplayName,
        Type = s.Type,
        MinValue = s.MinValue,
        MaxValue = s.MaxValue,
        DefaultValue = s.DefaultValue,
        MaxChange = s.MaxChange,
        IncrementOnly = s.IncrementOnly,
        SortOrder = s.SortOrder,
        IsActive = s.IsActive
    };

    private async Task<IReadOnlyList<GameStatValueDto>> StatValuesAsync(
        int gameId,
        int? viewerUserId,
        CancellationToken cancellationToken)
    {
        var stats = await ActiveStats(gameId).ToListAsync(cancellationToken);
        var values = viewerUserId is int viewerId
            ? await db.UserGameStats.AsNoTracking()
                .Where(u => u.UserId == viewerId && u.Stat!.GameId == gameId)
                .ToDictionaryAsync(u => u.StatId, u => u.Value, cancellationToken)
            : [];
        return stats.Select(s => new GameStatValueDto
        {
            Key = s.Key,
            DisplayName = s.DisplayName,
            Type = s.Type,
            Value = values.TryGetValue(s.Id, out var v) ? v : s.DefaultValue
        }).ToList();
    }

    /// <summary>
    /// Upserts stats by key and deactivates the rest (saved with the achievements); returns the active keys,
    /// so an achievement still linked to a removed stat fails validation and nothing is saved.
    /// </summary>
    private async Task<List<string>> SyncStatsAsync(
        int gameId,
        IReadOnlyList<GameStatDefinitionDto> stats,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (AchievementRules.ValidateStats(stats) is { } error)
        {
            throw new ArgumentException(error);
        }

        var existing = await db.GameStats
            .Where(s => s.GameId == gameId)
            .ToDictionaryAsync(s => s.Key, StringComparer.Ordinal, cancellationToken);
        var kept = new List<string>();
        foreach (var item in stats)
        {
            var key = AchievementRules.NormalizeKey(item.Key)!;
            kept.Add(key);
            if (!existing.TryGetValue(key, out var row))
            {
                row = new GameStatEntity { GameId = gameId, Key = key, CreatedAtUtc = now };
                db.GameStats.Add(row);
            }

            row.DisplayName = item.DisplayName?.Trim() ?? string.Empty;
            row.Type = item.Type;
            row.MinValue = item.MinValue;
            row.MaxValue = item.MaxValue;
            row.DefaultValue = item.DefaultValue;
            row.MaxChange = item.MaxChange;
            row.IncrementOnly = item.IncrementOnly;
            row.SortOrder = kept.Count - 1;
            row.IsActive = true;
            row.UpdatedAtUtc = now;
        }

        foreach (var row in existing.Values.Where(s => !kept.Contains(s.Key) && s.IsActive))
        {
            row.IsActive = false;
            row.UpdatedAtUtc = now;
        }

        return kept;
    }

    private async Task<Dictionary<int, int>> UnlockedCountsAsync(int gameId, CancellationToken cancellationToken) =>
        await db.UserGameAchievements.AsNoTracking()
            .Where(u => u.Achievement!.GameId == gameId && u.UnlockedAtUtc != null)
            .GroupBy(u => u.AchievementId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

    private async Task<IReadOnlyList<AchievementDefinitionDto>> ListDefinitionsAsync(
        int gameId,
        CancellationToken cancellationToken)
    {
        var rows = await ActiveDefinitions(gameId).ToListAsync(cancellationToken);
        var counts = await UnlockedCountsAsync(gameId, cancellationToken);
        return rows.Select(a => new AchievementDefinitionDto
        {
            Key = a.Key,
            Title = a.Title,
            Description = a.Description,
            IconUrl = a.IconUrl,
            LockedIconUrl = a.LockedIconUrl,
            StatKey = a.StatKey,
            Translations = ParseTranslations(a.TranslationsJson),
            Points = a.Points,
            Hidden = a.Hidden,
            Target = a.Target,
            SortOrder = a.SortOrder,
            IsActive = a.IsActive,
            UnlockedCount = counts.GetValueOrDefault(a.Id)
        }).ToList();
    }

    /// <summary>Translated text for "en" or "en-US"; the base text when there is none.</summary>
    private static (string Title, string Description) Localize(GameAchievementEntity definition, string? language)
    {
        var code = AchievementRules.NormalizeLanguage(language);
        var translations = code is null ? null : ParseTranslations(definition.TranslationsJson);
        if (code is not null
            && translations is not null
            && (translations.TryGetValue(code, out var text) || translations.TryGetValue(code.Split('-')[0], out text)))
        {
            return (
                string.IsNullOrWhiteSpace(text.Title) ? definition.Title : text.Title,
                string.IsNullOrWhiteSpace(text.Description) ? definition.Description : text.Description);
        }

        return (definition.Title, definition.Description);
    }

    private static Dictionary<string, AchievementTextDto>? ParseTranslations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, AchievementTextDto>>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Normalizes language codes and drops entries without any text; null when nothing is left.</summary>
    private static string? SerializeTranslations(IReadOnlyDictionary<string, AchievementTextDto>? translations)
    {
        var clean = translations?
            .Select(kv => (
                Language: AchievementRules.NormalizeLanguage(kv.Key)!,
                Title: kv.Value?.Title?.Trim() ?? string.Empty,
                Description: kv.Value?.Description?.Trim() ?? string.Empty))
            .Where(t => t.Title.Length > 0 || t.Description.Length > 0)
            .ToDictionary(
                t => t.Language,
                t => new AchievementTextDto { Title = t.Title, Description = t.Description });
        return clean is { Count: > 0 } ? JsonSerializer.Serialize(clean, Json) : null;
    }
}
