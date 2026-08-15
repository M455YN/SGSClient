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
    public async Task<IReadOnlyList<GameDto>> GetPublishedGamesAsync(CancellationToken cancellationToken = default)
    {
        return await db.Games
            .AsNoTracking()
            .Where(g => !g.DraftP && g.Kind == GameContentKind.Game)
            .OrderByDescending(g => g.FeaturedP)
            .ThenBy(g => g.Title)
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
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == userId, cancellationToken);
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

        var userIds = games.Select(g => g.UserId).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return games;
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

        return games;
    }

    public async Task<GameDto?> UpdateGameAsAdminAsync(
        int gameId,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
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
                IsCurrent = v.Version == build.CurrentVersion
            })
            .ToListAsync(cancellationToken);
    }

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
        CancellationToken cancellationToken)
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

        if (request.Genre is not null)
        {
            game.TypeId = await ResolveTaxonomyIdAsync(
                request.Genre,
                isGenre: true,
                cancellationToken);
        }

        if (request.Engine is not null)
        {
            game.EngineId = await ResolveTaxonomyIdAsync(
                request.Engine,
                isGenre: false,
                cancellationToken);
        }

        if (request.DraftP is not null)
        {
            game.DraftP = request.DraftP.Value;
        }

        if (request.FeaturedP is not null)
        {
            game.FeaturedP = request.FeaturedP.Value;
        }
    }

    private async Task<int?> ResolveTaxonomyIdAsync(string raw, bool isGenre, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var symbol = SgsGameTaxonomy.NormalizeSlug(raw)
            ?? throw new ArgumentException(isGenre ? "Nieprawidłowy gatunek." : "Nieprawidłowy silnik.");

        if (isGenre)
        {
            if (!SgsGameTaxonomy.IsValidGenre(symbol))
            {
                throw new ArgumentException($"Nieznany gatunek '{raw}'.");
            }

            var id = await db.GameGenres
                .Where(g => g.Symbol == symbol)
                .Select(g => (int?)g.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return id ?? throw new ArgumentException($"Gatunek '{symbol}' nie istnieje w bazie.");
        }

        if (!SgsGameTaxonomy.IsValidEngine(symbol))
        {
            throw new ArgumentException($"Nieznany silnik '{raw}'.");
        }

        var engineId = await db.GameEngines
            .Where(e => e.Symbol == symbol)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return engineId ?? throw new ArgumentException($"Silnik '{symbol}' nie istnieje w bazie.");
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
            EngineId = g.EngineId,
            TypeId = g.TypeId,
            GenreSymbol = g.Genre != null ? g.Genre.Symbol : null,
            GenreName = g.Genre != null ? g.Genre.Name : null,
            EngineSymbol = g.Engine != null ? g.Engine.Symbol : null,
            EngineName = g.Engine != null ? g.Engine.Name : null,
            CoverUrl = g.CoverUrl,
            IntroUrl = g.IntroUrl,
            ScreenshotUrls = g.Screenshots
                .OrderBy(s => s.SortOrder)
                .Select(s => s.Url)
                .ToList(),
            DraftP = g.DraftP,
            FeaturedP = g.FeaturedP,
            Kind = g.Kind,
            ParentGameId = g.ParentGameId,
            ParentSymbol = g.ParentGame != null ? g.ParentGame.Symbol : null,
            CreatedAtUtc = g.CreatedAtUtc,
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
