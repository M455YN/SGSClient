using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence;

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
            .Select(g => new GameDto
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
            })
            .ToListAsync(cancellationToken);
    }
}
