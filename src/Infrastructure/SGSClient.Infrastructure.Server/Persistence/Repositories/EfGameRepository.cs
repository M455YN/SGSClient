using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Repositories;

public sealed class EfGameRepository(SgsServerDbContext db) : IGameRepository
{
    public async Task<IReadOnlyList<GameDto>> GetPublishedGamesAsync(CancellationToken cancellationToken = default)
    {
        return await db.Games
            .AsNoTracking()
            .Where(g => !g.DraftP)
            .OrderByDescending(g => g.FeaturedP)
            .ThenBy(g => g.Title)
            .Select(ProjectGame())
            .ToListAsync(cancellationToken);
    }

    public async Task<GameDto?> GetPublishedGameByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.Games
            .AsNoTracking()
            .Where(g => g.Id == id && !g.DraftP)
            .Select(ProjectGame())
            .FirstOrDefaultAsync(cancellationToken);
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
