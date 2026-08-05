using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Enums;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence;

public static class SgsServerDbInitializer
{
    public static async Task InitializeAsync(SgsServerDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await EnsureTaxonomyAsync(db, cancellationToken);

        if (await db.Games.AnyAsync(cancellationToken))
        {
            return;
        }

        var actionGenreId = await db.GameGenres
            .Where(g => g.Symbol == "action")
            .Select(g => g.Id)
            .FirstAsync(cancellationToken);
        var adventureGenreId = await db.GameGenres
            .Where(g => g.Symbol == "adventure")
            .Select(g => g.Id)
            .FirstAsync(cancellationToken);
        var customEngineId = await db.GameEngines
            .Where(e => e.Symbol == "custom")
            .Select(e => e.Id)
            .FirstAsync(cancellationToken);

        var demoRunner = new GameEntity
        {
            UserId = 1,
            Title = "Demo Runner",
            Symbol = "demo-runner",
            Description = "Sample catalog game for local development.",
            HardwareRequirements = "RAM: 4 GB",
            OtherInformation = null,
            EngineId = customEngineId,
            TypeId = actionGenreId,
            DraftP = false,
            FeaturedP = true,
            Builds =
            [
                new GameBuildEntity
                {
                    Platform = GamePlatform.Windows,
                    Architecture = "x64",
                    CurrentVersion = "0.1.0",
                    ExeName = "DemoRunner.exe",
                    ZipLink = "https://example.com/games/demo-runner-win-x64.zip",
                    IsDefault = true
                },
                new GameBuildEntity
                {
                    Platform = GamePlatform.Linux,
                    Architecture = "x64",
                    CurrentVersion = "0.1.0",
                    ExeName = "DemoRunner",
                    ZipLink = "https://example.com/games/demo-runner-linux-x64.zip",
                    IsDefault = true
                }
            ]
        };

        var skyArena = new GameEntity
        {
            UserId = 1,
            Title = "Sky Arena",
            Symbol = "sky-arena",
            Description = "Another sample title seeded for API smoke tests.",
            HardwareRequirements = "RAM: 8 GB",
            OtherInformation = "Early access notes.",
            EngineId = customEngineId,
            TypeId = adventureGenreId,
            DraftP = false,
            FeaturedP = false,
            Builds =
            [
                new GameBuildEntity
                {
                    Platform = GamePlatform.Windows,
                    Architecture = "x64",
                    CurrentVersion = "1.0.0",
                    ExeName = "SkyArena.exe",
                    ZipLink = "https://example.com/games/sky-arena-win-x64.zip",
                    IsDefault = true
                },
                new GameBuildEntity
                {
                    Platform = GamePlatform.MacOS,
                    Architecture = "arm64",
                    CurrentVersion = "1.0.0",
                    ExeName = "SkyArena.app",
                    ZipLink = "https://example.com/games/sky-arena-osx-arm64.zip",
                    IsDefault = true
                }
            ]
        };

        db.Games.AddRange(demoRunner, skyArena);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureTaxonomyAsync(SgsServerDbContext db, CancellationToken cancellationToken = default)
    {
        foreach (var item in SgsGameTaxonomy.Genres)
        {
            if (!await db.GameGenres.AnyAsync(g => g.Symbol == item.Symbol, cancellationToken))
            {
                db.GameGenres.Add(new GameGenreEntity { Symbol = item.Symbol, Name = item.Name });
            }
        }

        foreach (var item in SgsGameTaxonomy.Engines)
        {
            if (!await db.GameEngines.AnyAsync(e => e.Symbol == item.Symbol, cancellationToken))
            {
                db.GameEngines.Add(new GameEngineEntity { Symbol = item.Symbol, Name = item.Name });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgsServerDbContext>();
        await InitializeAsync(db, cancellationToken);
    }
}
