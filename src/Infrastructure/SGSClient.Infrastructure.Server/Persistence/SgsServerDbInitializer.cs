using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
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

        if (!await db.Games.AnyAsync(cancellationToken))
        {
            await SeedCatalogAsync(db, cancellationToken);
        }
    }

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgsServerDbContext>();
        await InitializeAsync(db, cancellationToken);

        var storage = scope.ServiceProvider.GetRequiredService<IOptions<GameStorageSettings>>();
        await SeedGamePackageWriter.EnsurePackagesAsync(db, storage, cancellationToken);
    }

    private static async Task SeedCatalogAsync(SgsServerDbContext db, CancellationToken cancellationToken)
    {
        var actionGenre = await db.GameGenres
            .Where(g => g.Symbol == "action")
            .FirstAsync(cancellationToken);
        var adventureGenre = await db.GameGenres
            .Where(g => g.Symbol == "adventure")
            .FirstAsync(cancellationToken);
        var customEngine = await db.GameEngines
            .Where(e => e.Symbol == "custom")
            .FirstAsync(cancellationToken);

        // ZipLink placeholders — SeedGamePackageWriter rewrites them to real storage packages.
        var demoRunner = new GameEntity
        {
            UserId = 1,
            Title = "Demo Runner",
            Symbol = "demo-runner",
            Description = "Sample catalog game for local development (claim → install → play).",
            HardwareRequirements = "RAM: 4 GB",
            OtherInformation = null,
            Engines = [customEngine],
            Genres = [actionGenre],
            DraftP = false,
            FeaturedP = true,
            Builds =
            [
                new GameBuildEntity
                {
                    Platform = GamePlatform.Windows,
                    Architecture = "x64",
                    CurrentVersion = "0.1.0",
                    ExeName = "bin/DemoRunner.cmd",
                    ZipLink = "/api/games/packages/demo-runner/0.1.0/windows-x64.zip",
                    IsDefault = true
                },
                new GameBuildEntity
                {
                    Platform = GamePlatform.Linux,
                    Architecture = "x64",
                    CurrentVersion = "0.1.0",
                    ExeName = "bin/DemoRunner.sh",
                    ZipLink = "/api/games/packages/demo-runner/0.1.0/linux-x64.zip",
                    IsDefault = true
                }
            ]
        };

        var skyArena = new GameEntity
        {
            UserId = 1,
            Title = "Sky Arena",
            Symbol = "sky-arena",
            Description = "Another sample title with a seed package for smoke tests.",
            HardwareRequirements = "RAM: 8 GB",
            OtherInformation = "Early access notes.",
            Engines = [customEngine],
            Genres = [adventureGenre, actionGenre],
            DraftP = false,
            FeaturedP = false,
            Builds =
            [
                new GameBuildEntity
                {
                    Platform = GamePlatform.Windows,
                    Architecture = "x64",
                    CurrentVersion = "1.0.0",
                    ExeName = "bin/DemoRunner.cmd",
                    ZipLink = "/api/games/packages/sky-arena/1.0.0/windows-x64.zip",
                    IsDefault = true
                },
                new GameBuildEntity
                {
                    Platform = GamePlatform.MacOS,
                    Architecture = "arm64",
                    CurrentVersion = "1.0.0",
                    ExeName = "bin/DemoRunner.sh",
                    ZipLink = "/api/games/packages/sky-arena/1.0.0/macos-arm64.zip",
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
}
