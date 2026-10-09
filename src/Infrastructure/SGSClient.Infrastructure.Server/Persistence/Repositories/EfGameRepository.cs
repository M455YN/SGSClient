using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Repositories;

public sealed class EfGameRepository(
    SgsServerDbContext db,
    IOptions<GameStorageSettings> storageOptions) : IGameRepository
{
    public async Task<IReadOnlyList<GameDto>> GetPublishedGamesAsync(
        GameCatalogQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var games = db.Games
            .AsNoTracking()
            .Where(g => !g.DraftP && g.Kind == GameContentKind.Game);

        if (query is not null)
        {
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var q = query.Q.Trim();
                games = games.Where(g =>
                    g.Title.Contains(q)
                    || g.Symbol.Contains(q)
                    || (g.StudioName != null && g.StudioName.Contains(q))
                    || (g.TagsCsv != null && g.TagsCsv.Contains(q)));
            }

            if (!string.IsNullOrWhiteSpace(query.Genre))
            {
                var genre = query.Genre.Trim();
                games = games.Where(g => g.Genres.Any(x =>
                    x.Name == genre
                    || x.Symbol == genre
                    || x.Name.Contains(genre)
                    || x.Symbol.Contains(genre)));
            }

            if (!string.IsNullOrWhiteSpace(query.Tag))
            {
                var tag = query.Tag.Trim().ToLowerInvariant();
                games = games.Where(g => g.TagsCsv != null && g.TagsCsv.ToLower().Contains(tag));
            }

            if (query.ReleaseStatus is { } status)
            {
                games = games.Where(g => g.ReleaseStatus == status);
            }

            if (query.Featured is { } featured)
            {
                games = games.Where(g => g.FeaturedP == featured);
            }
        }

        var sort = query?.Sort?.Trim().ToLowerInvariant();
        IOrderedQueryable<GameEntity> ordered = sort switch
        {
            "title" => games.OrderBy(g => g.Title),
            "newest" => games.OrderByDescending(g => g.CreatedAtUtc).ThenBy(g => g.Title),
            _ => games
                .OrderByDescending(g => g.FeaturedP)
                .ThenBy(g => g.FeaturedOrder ?? int.MaxValue)
                .ThenBy(g => g.Title)
        };

        return await ordered
            .Select(ProjectGame(includeDlcs: false))
            .ToListAsync(cancellationToken);
    }

    public async Task<GameDto?> GetPublishedGameByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.Games
            .AsNoTracking()
            .Where(g => g.Id == id && !g.DraftP)
            .Select(ProjectGame(includeDlcs: true))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameDlcDto>> GetPublishedDlcsAsync(
        int parentGameId,
        CancellationToken cancellationToken = default)
    {
        var parentExists = await db.Games.AsNoTracking()
            .AnyAsync(g => g.Id == parentGameId && !g.DraftP && g.Kind == GameContentKind.Game, cancellationToken);
        if (!parentExists)
        {
            return [];
        }

        return await db.Games
            .AsNoTracking()
            .Where(g => g.ParentGameId == parentGameId && g.Kind == GameContentKind.Dlc && !g.DraftP)
            .OrderBy(g => g.Title)
            .Select(ProjectDlc())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameDto>> GetOwnedGamesAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await db.Games
            .AsNoTracking()
            .Where(g => g.UserId == userId)
            .OrderBy(g => g.Kind)
            .ThenBy(g => g.Title)
            .Select(ProjectGame(includeDlcs: true))
            .ToListAsync(cancellationToken);
    }

    public async Task<GameDto?> UpdateOwnedGameAsync(
        int gameId,
        int userId,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var game = await db.Games.Include(g => g.Genres).Include(g => g.Engines).FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);
        if (game is null)
        {
            return null;
        }

        await ApplyMetadataUpdatesAsync(game, request, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(ProjectGame(includeDlcs: true))
            .FirstAsync(cancellationToken);
    }

    public async Task<bool> DeleteOwnedGameAsync(int gameId, int userId, CancellationToken cancellationToken = default)
    {
        var game = await db.Games
            .Include(g => g.Dlcs)
            .FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);
        if (game is null)
        {
            return false;
        }

        if (game.Kind == GameContentKind.Game && game.Dlcs.Count > 0)
        {
            throw new InvalidOperationException("Najpierw usuń powiązane DLC.");
        }

        var symbol = game.Symbol;
        db.Games.Remove(game);
        await db.SaveChangesAsync(cancellationToken);

        TryDeleteStorageFolder(symbol);
        return true;
    }

    public async Task<IReadOnlyList<GameDto>> GetAllGamesForAdminAsync(CancellationToken cancellationToken = default)
    {
        var games = await db.Games
            .AsNoTracking()
            .OrderBy(g => g.Kind)
            .ThenBy(g => g.Title)
            .Select(ProjectGame(includeDlcs: true))
            .ToListAsync(cancellationToken);

        await AttachOwnersAsync(games, cancellationToken);
        return games;
    }

    public async Task<GameDto?> UpdateGameAsAdminAsync(
        int gameId,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var game = await db.Games.Include(g => g.Genres).Include(g => g.Engines).FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null)
        {
            return null;
        }

        await ApplyMetadataUpdatesAsync(game, request, cancellationToken, allowCuration: true);
        await db.SaveChangesAsync(cancellationToken);
        var dto = await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(ProjectGame(includeDlcs: true))
            .FirstAsync(cancellationToken);
        await AttachOwnersAsync([dto], cancellationToken);
        return dto;
    }

    public async Task<bool> DeleteGameAsAdminAsync(int gameId, CancellationToken cancellationToken = default)
    {
        var game = await db.Games
            .Include(g => g.Dlcs)
            .FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null)
        {
            return false;
        }

        if (game.Kind == GameContentKind.Game && game.Dlcs.Count > 0)
        {
            throw new InvalidOperationException("Najpierw usuń powiązane DLC.");
        }

        var symbol = game.Symbol;
        db.Games.Remove(game);
        await db.SaveChangesAsync(cancellationToken);

        TryDeleteStorageFolder(symbol);
        return true;
    }

    public async Task<IReadOnlyList<GameBuildVersionDto>> GetBuildVersionHistoryAsync(
        int gameId,
        int buildId,
        CancellationToken cancellationToken = default)
    {
        var build = await db.GameBuilds
            .AsNoTracking()
            .Where(b => b.Id == buildId && b.GameId == gameId)
            .Select(b => new { b.Id, b.CurrentVersion, b.Game!.DraftP })
            .FirstOrDefaultAsync(cancellationToken);

        if (build is null || build.DraftP)
        {
            return [];
        }

        return await db.GameBuildVersions
            .AsNoTracking()
            .Where(v => v.GameBuildId == buildId)
            .OrderByDescending(v => v.PublishedAtUtc)
            .Select(v => new GameBuildVersionDto
            {
                Id = v.Id,
                GameBuildId = v.GameBuildId,
                Version = v.Version,
                ExeName = v.ExeName,
                ZipLink = v.ZipLink,
                PublishedAtUtc = v.PublishedAtUtc,
                IsCurrent = v.Version == build.CurrentVersion,
                ReleaseNotes = v.ReleaseNotes
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameBuildVersionDto>> GetOwnedVersionHistoryAsync(
        int gameId,
        int ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var owns = await db.Games
            .AsNoTracking()
            .AnyAsync(g => g.Id == gameId && g.UserId == ownerUserId, cancellationToken);
        return owns ? await QueryVersionHistoryAsync(gameId, cancellationToken) : [];
    }

    public async Task<IReadOnlyList<GameBuildVersionDto>> GetVersionHistoryAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Games.AsNoTracking().AnyAsync(g => g.Id == gameId, cancellationToken);
        return exists ? await QueryVersionHistoryAsync(gameId, cancellationToken) : [];
    }

    private async Task<IReadOnlyList<GameBuildVersionDto>> QueryVersionHistoryAsync(
        int gameId,
        CancellationToken cancellationToken)
    {
        var rows = await db.GameBuildVersions
            .AsNoTracking()
            .Where(v => v.GameBuild!.GameId == gameId)
            .OrderByDescending(v => v.PublishedAtUtc)
            .Select(v => new
            {
                v.Id,
                v.GameBuildId,
                v.Version,
                v.ExeName,
                v.ZipLink,
                v.PublishedAtUtc,
                v.ReleaseNotes,
                CurrentVersion = v.GameBuild!.CurrentVersion,
                Platform = v.GameBuild.Platform,
                Architecture = v.GameBuild.Architecture
            })
            .ToListAsync(cancellationToken);

        return rows.Select(v => new GameBuildVersionDto
        {
            Id = v.Id,
            GameBuildId = v.GameBuildId,
            Version = v.Version,
            ExeName = v.ExeName,
            ZipLink = v.ZipLink,
            PublishedAtUtc = v.PublishedAtUtc,
            IsCurrent = v.Version == v.CurrentVersion,
            ReleaseNotes = v.ReleaseNotes,
            PlatformLabel = string.IsNullOrWhiteSpace(v.Architecture)
                ? v.Platform.ToString()
                : $"{v.Platform} {v.Architecture}"
        }).ToList();
    }

    public Task<GameBuildVersionDto?> UpdateOwnedVersionNotesAsync(
        int gameId,
        int versionId,
        int ownerUserId,
        string? releaseNotes,
        CancellationToken cancellationToken = default) =>
        UpdateVersionNotesCoreAsync(gameId, versionId, ownerUserId, releaseNotes, cancellationToken);

    public Task<GameBuildVersionDto?> UpdateVersionNotesAsAdminAsync(
        int gameId,
        int versionId,
        string? releaseNotes,
        CancellationToken cancellationToken = default) =>
        UpdateVersionNotesCoreAsync(gameId, versionId, ownerUserId: null, releaseNotes, cancellationToken);

    private async Task<GameBuildVersionDto?> UpdateVersionNotesCoreAsync(
        int gameId,
        int versionId,
        int? ownerUserId,
        string? releaseNotes,
        CancellationToken cancellationToken)
    {
        var version = await db.GameBuildVersions
            .Include(v => v.GameBuild)
            .ThenInclude(b => b!.Game)
            .FirstOrDefaultAsync(
                v => v.Id == versionId
                    && v.GameBuild!.GameId == gameId
                    && (ownerUserId == null || v.GameBuild.Game!.UserId == ownerUserId),
                cancellationToken);
        if (version is null)
        {
            return null;
        }

        var notes = releaseNotes?.Trim();
        if (notes?.Length > SgsPackageRules.MaxReleaseNotesLength)
        {
            throw new ArgumentException(
                $"Opis zmian może mieć maksymalnie {SgsPackageRules.MaxReleaseNotesLength} znaków.");
        }

        version.ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        await db.SaveChangesAsync(cancellationToken);

        var build = version.GameBuild!;
        return new GameBuildVersionDto
        {
            Id = version.Id,
            GameBuildId = version.GameBuildId,
            Version = version.Version,
            ExeName = version.ExeName,
            ZipLink = version.ZipLink,
            PublishedAtUtc = version.PublishedAtUtc,
            IsCurrent = version.Version == build.CurrentVersion,
            ReleaseNotes = version.ReleaseNotes,
            PlatformLabel = build.Architecture is null
                ? build.Platform.ToString()
                : $"{build.Platform} {build.Architecture}"
        };
    }

    public async Task<GameStatsDto?> GetOwnedGameStatsAsync(
        int gameId,
        int ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var owns = await db.Games
            .AsNoTracking()
            .AnyAsync(g => g.Id == gameId && g.UserId == ownerUserId, cancellationToken);
        return owns ? await QueryGameStatsAsync(gameId, cancellationToken) : null;
    }

    public async Task<GameStatsDto?> GetGameStatsAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        var exists = await db.Games.AsNoTracking().AnyAsync(g => g.Id == gameId, cancellationToken);
        return exists ? await QueryGameStatsAsync(gameId, cancellationToken) : null;
    }

    private async Task<GameStatsDto> QueryGameStatsAsync(int gameId, CancellationToken cancellationToken)
    {
        var today = DateTimeOffset.UtcNow.Date;
        var from30 = new DateTimeOffset(today.AddDays(-29), TimeSpan.Zero);
        var from7 = new DateTimeOffset(today.AddDays(-6), TimeSpan.Zero);

        var downloads = await db.GameDownloads
            .AsNoTracking()
            .Where(d => d.GameId == gameId)
            .Select(d => new { d.Version, d.DownloadedAtUtc })
            .ToListAsync(cancellationToken);

        var perDay = downloads
            .Where(d => d.DownloadedAtUtc >= from30)
            .GroupBy(d => DateOnly.FromDateTime(d.DownloadedAtUtc.UtcDateTime.Date))
            .ToDictionary(g => g.Key, g => g.Count());

        var daily = Enumerable.Range(0, 30)
            .Select(offset =>
            {
                var day = DateOnly.FromDateTime(today.AddDays(offset - 29));
                return new GameStatsPointDto
                {
                    Day = day,
                    Downloads = perDay.TryGetValue(day, out var count) ? count : 0
                };
            })
            .ToList();

        return new GameStatsDto
        {
            GameId = gameId,
            TotalDownloads = downloads.Count,
            DownloadsLast30Days = downloads.Count(d => d.DownloadedAtUtc >= from30),
            DownloadsLast7Days = downloads.Count(d => d.DownloadedAtUtc >= from7),
            LastDownloadUtc = downloads.Count == 0 ? null : downloads.Max(d => d.DownloadedAtUtc),
            Daily = daily,
            ByVersion = downloads
                .GroupBy(d => d.Version)
                .Select(g => new GameStatsVersionDto { Version = g.Key, Downloads = g.Count() })
                .OrderByDescending(v => v.Downloads)
                .ToList()
        };
    }

    public async Task RecordDownloadAsync(
        string symbol,
        string version,
        string platformKey,
        CancellationToken cancellationToken = default)
    {
        var gameId = await db.Games
            .AsNoTracking()
            .Where(g => g.Symbol == symbol)
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (gameId is null)
        {
            return;
        }

        db.GameDownloads.Add(new GameDownloadEntity
        {
            GameId = gameId.Value,
            Version = Truncate(version, 64),
            PlatformKey = Truncate(platformKey, 64),
            DownloadedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private void TryDeleteStorageFolder(string symbol)
    {
        try
        {
            var root = Path.GetFullPath(storageOptions.Value.StoragePath);
            var path = Path.GetFullPath(Path.Combine(root, symbol));
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private async Task ApplyMetadataUpdatesAsync(
        GameEntity game,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken,
        bool allowCuration = false)
    {
        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (title.Length is < 2 or > 128)
            {
                throw new ArgumentException("Tytuł musi mieć 2-128 znaków.");
            }

            game.Title = title;
        }

        if (request.Description is not null)
        {
            game.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.HardwareRequirements is not null)
        {
            game.HardwareRequirements = string.IsNullOrWhiteSpace(request.HardwareRequirements)
                ? null
                : request.HardwareRequirements.Trim();
        }

        if (request.OtherInformation is not null)
        {
            game.OtherInformation = string.IsNullOrWhiteSpace(request.OtherInformation)
                ? null
                : request.OtherInformation.Trim();
        }

        var genreRequest = request.Genres ?? (request.Genre is null ? null : SgsGameTaxonomy.MergeSlugs(request.Genre, null));
        if (genreRequest is not null)
        {
            var slugs = SgsGameTaxonomy.MergeSlugs(null, genreRequest);
            await ReplaceTaxonomyAsync(game.Genres, db.GameGenres, slugs, x => x.Symbol, SgsGameTaxonomy.IsValidGenre,
                SgsGameTaxonomy.MaxGenres, "gatunek", "gatunków", cancellationToken);
        }

        var engineRequest = request.Engines ?? (request.Engine is null ? null : SgsGameTaxonomy.MergeSlugs(request.Engine, null));
        if (engineRequest is not null)
        {
            var slugs = SgsGameTaxonomy.MergeSlugs(null, engineRequest);
            await ReplaceTaxonomyAsync(game.Engines, db.GameEngines, slugs, x => x.Symbol, SgsGameTaxonomy.IsValidEngine,
                SgsGameTaxonomy.MaxEngines, "silnik", "silników", cancellationToken);
        }

        if (request.DraftP is not null)
        {
            game.DraftP = request.DraftP.Value;
        }

        if (request.StudioName is not null)
        {
            game.StudioName = Trimmed(request.StudioName, 128);
        }

        if (request.WebsiteUrl is not null)
        {
            game.WebsiteUrl = ValidatedUrl(request.WebsiteUrl, "Adres strony");
        }

        if (request.DiscordUrl is not null)
        {
            game.DiscordUrl = ValidatedUrl(request.DiscordUrl, "Adres Discorda");
        }

        if (request.SupportUrl is not null)
        {
            game.SupportUrl = ValidatedUrl(request.SupportUrl, "Adres wsparcia");
        }

        if (request.ReleaseStatus is not null)
        {
            if (!Enum.IsDefined(request.ReleaseStatus.Value))
            {
                throw new ArgumentException("Nieznany status wydania.");
            }

            game.ReleaseStatus = request.ReleaseStatus.Value;
        }

        if (request.ClearReleaseDate)
        {
            game.ReleaseDateUtc = null;
        }
        else if (request.ReleaseDateUtc is not null)
        {
            game.ReleaseDateUtc = request.ReleaseDateUtc;
        }

        if (request.AgeRating is not null)
        {
            if (!Enum.IsDefined(request.AgeRating.Value))
            {
                throw new ArgumentException("Nieznana kategoria wiekowa.");
            }

            game.AgeRating = request.AgeRating.Value;
        }

        if (request.Features is not null)
        {
            game.Features = request.Features.Value;
        }

        if (request.Languages is not null)
        {
            game.LanguagesCsv = CsvList.Join(request.Languages);
        }

        if (request.Tags is not null)
        {
            game.TagsCsv = CsvList.Join(request.Tags.Select(t => t.ToLowerInvariant()));
        }

        if (!allowCuration)
        {
            return;
        }

        if (request.FeaturedP is not null)
        {
            game.FeaturedP = request.FeaturedP.Value;
            if (!game.FeaturedP)
            {
                game.FeaturedOrder = null;
            }
        }

        if (request.FeaturedOrder is not null)
        {
            game.FeaturedOrder = request.FeaturedOrder.Value < 0 ? null : request.FeaturedOrder.Value;
        }

        if (request.OwnerUserId is int ownerUserId)
        {
            if (ownerUserId <= 0)
            {
                throw new ArgumentException("Nieprawidłowy identyfikator właściciela.");
            }

            var exists = await db.Users.AnyAsync(u => u.Id == ownerUserId, cancellationToken);
            if (!exists)
            {
                throw new ArgumentException("Wybrany użytkownik nie istnieje.");
            }

            game.UserId = ownerUserId;
        }
    }

    private async Task AttachOwnersAsync(IReadOnlyList<GameDto> games, CancellationToken cancellationToken)
    {
        var userIds = games.Select(g => g.UserId).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        var owners = await db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken);
        var byId = owners.ToDictionary(u => u.Id);

        foreach (var game in games)
        {
            if (byId.TryGetValue(game.UserId, out var owner))
            {
                game.OwnerDisplayName = owner.DisplayName;
                game.OwnerEmail = owner.Email;
            }
        }
    }

    private static string? Trimmed(string value, int maxLength)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? ValidatedUrl(string value, string label)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException($"{label} musi zaczynać się od http:// lub https://.");
        }

        return trimmed.Length <= 2048 ? trimmed : throw new ArgumentException($"{label} jest za długi.");
    }

    private static async Task ReplaceTaxonomyAsync<T>(
        ICollection<T> target,
        IQueryable<T> source,
        IReadOnlyList<string> slugs,
        Func<T, string> symbolOf,
        Func<string?, bool> isValid,
        int max,
        string singular,
        string genitivePlural,
        CancellationToken cancellationToken)
        where T : class
    {
        if (slugs.Count > max)
        {
            throw new ArgumentException($"Maksymalnie {max} {genitivePlural} na grę.");
        }

        var bad = slugs.FirstOrDefault(s => !isValid(s));
        if (bad is not null)
        {
            throw new ArgumentException($"Nieznany {singular} '{bad}'.");
        }

        var all = await source.ToListAsync(cancellationToken);
        var wanted = all.Where(x => slugs.Contains(symbolOf(x))).ToList();
        if (wanted.Count != slugs.Count)
        {
            throw new ArgumentException($"Nie wszystkie wartości ({singular}) istnieją w bazie.");
        }

        target.Clear();
        foreach (var item in wanted)
        {
            target.Add(item);
        }
    }

    private static System.Linq.Expressions.Expression<Func<GameEntity, GameDto>> ProjectGame(bool includeDlcs) =>
        g => new GameDto
        {
            Id = g.Id,
            UserId = g.UserId,
            Title = g.Title,
            Symbol = g.Symbol,
            Description = g.Description,
            HardwareRequirements = g.HardwareRequirements,
            OtherInformation = g.OtherInformation,
            Genres = g.Genres.OrderBy(x => x.Id).Select(x => new SgsGameTaxonomy.TaxonomyItem(x.Symbol, x.Name)).ToList(),
            Engines = g.Engines.OrderBy(x => x.Id).Select(x => new SgsGameTaxonomy.TaxonomyItem(x.Symbol, x.Name)).ToList(),
            CoverUrl = g.CoverUrl,
            HeaderUrl = g.HeaderUrl,
            LogoUrl = g.LogoUrl,
            IntroUrl = g.IntroUrl,
            ScreenshotUrls = g.Screenshots
                .OrderBy(s => s.SortOrder)
                .Select(s => s.Url)
                .ToList(),
            Screenshots = g.Screenshots
                .OrderBy(s => s.SortOrder)
                .Select(s => new GameScreenshotDto { Id = s.Id, Url = s.Url })
                .ToList(),
            DraftP = g.DraftP,
            FeaturedP = g.FeaturedP,
            FeaturedOrder = g.FeaturedOrder,
            Kind = g.Kind,
            ParentGameId = g.ParentGameId,
            ParentSymbol = g.ParentGame != null ? g.ParentGame.Symbol : null,
            CreatedAtUtc = g.CreatedAtUtc,
            StudioName = g.StudioName,
            WebsiteUrl = g.WebsiteUrl,
            DiscordUrl = g.DiscordUrl,
            SupportUrl = g.SupportUrl,
            ReleaseStatus = g.ReleaseStatus,
            ReleaseDateUtc = g.ReleaseDateUtc,
            AgeRating = g.AgeRating,
            Features = g.Features,
            LanguagesCsv = g.LanguagesCsv,
            TagsCsv = g.TagsCsv,
            PackageSizeBytes = g.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.PackageSizeBytes)
                .FirstOrDefault(),
            Builds = g.Builds
                .OrderByDescending(b => b.IsDefault)
                .ThenBy(b => b.Platform)
                .Select(b => new GameBuildDto
                {
                    Id = b.Id,
                    GameId = b.GameId,
                    Platform = b.Platform,
                    Architecture = b.Architecture,
                    CurrentVersion = b.CurrentVersion,
                    ExeName = b.ExeName,
                    ZipLink = b.ZipLink,
                    IsDefault = b.IsDefault
                })
                .ToList(),
            Dlcs = includeDlcs && g.Kind == GameContentKind.Game
                ? g.Dlcs
                    .Where(d => d.Kind == GameContentKind.Dlc && !d.DraftP)
                    .OrderBy(d => d.Title)
                    .Select(d => new GameDlcDto
                    {
                        Id = d.Id,
                        ParentGameId = d.ParentGameId ?? g.Id,
                        Title = d.Title,
                        Symbol = d.Symbol,
                        CoverUrl = d.CoverUrl,
                        PackageSizeBytes = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.PackageSizeBytes)
                            .FirstOrDefault(),
                        CreatedAtUtc = d.CreatedAtUtc,
                        PreferredBuildId = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.Id)
                            .FirstOrDefault(),
                        PreferredPlatform = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.Platform)
                            .FirstOrDefault(),
                        PreferredArchitecture = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.Architecture)
                            .FirstOrDefault(),
                        PreferredVersion = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.CurrentVersion)
                            .FirstOrDefault()
                    })
                    .ToList()
                : new List<GameDlcDto>()
        };

    private static System.Linq.Expressions.Expression<Func<GameEntity, GameDlcDto>> ProjectDlc() =>
        d => new GameDlcDto
        {
            Id = d.Id,
            ParentGameId = d.ParentGameId ?? 0,
            Title = d.Title,
            Symbol = d.Symbol,
            CoverUrl = d.CoverUrl,
            PackageSizeBytes = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.PackageSizeBytes)
                .FirstOrDefault(),
            CreatedAtUtc = d.CreatedAtUtc,
            PreferredBuildId = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.Id)
                .FirstOrDefault(),
            PreferredPlatform = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.Platform)
                .FirstOrDefault(),
            PreferredArchitecture = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.Architecture)
                .FirstOrDefault(),
            PreferredVersion = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.CurrentVersion)
                .FirstOrDefault()
        };
}
