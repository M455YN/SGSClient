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
            .Select(ProjectGame())
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
            .Select(ProjectGame())
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

    private static System.Linq.Expressions.Expression<Func<GameEntity, GameDto>> ProjectGame() =>
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
                .ToList()
        };
}
