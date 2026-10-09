using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Persistence.Repositories;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;

namespace SGSClient.Server.Tests;

public class GameCatalogQueryTests
{
    [Fact]
    public async Task GetPublishedGamesAsync_FiltersByQueryGenreAndSort()
    {
        await using var db = CreateDb();
        var rpg = new GameGenreEntity { Id = 1, Symbol = "rpg", Name = "RPG" };
        var action = new GameGenreEntity { Id = 2, Symbol = "action", Name = "Action" };
        db.GameGenres.AddRange(rpg, action);
        db.Games.Add(MakeGame(1, "Alpha Quest", "alpha", featured: true, genres: [rpg], createdOffsetDays: 3));
        db.Games.Add(MakeGame(2, "Beta Blast", "beta", featured: false, genres: [action], createdOffsetDays: 1));
        db.Games.Add(MakeGame(3, "Gamma RPG", "gamma", featured: false, genres: [rpg, action], createdOffsetDays: 2));
        await db.SaveChangesAsync();

        var repo = new EfGameRepository(db, Options.Create(new GameStorageSettings { StoragePath = "." }));

        var byQ = await repo.GetPublishedGamesAsync(new GameCatalogQuery { Q = "beta" });
        Assert.Single(byQ);
        Assert.Equal("Beta Blast", byQ[0].Title);

        var byGenre = await repo.GetPublishedGamesAsync(new GameCatalogQuery { Genre = "RPG" });
        Assert.Equal(2, byGenre.Count);

        var byAction = await repo.GetPublishedGamesAsync(new GameCatalogQuery { Genre = "action" });
        Assert.Equal(2, byAction.Count);
        Assert.Contains(byAction, g => g.Title == "Gamma RPG" && g.Genres.Count == 2);

        var newest = await repo.GetPublishedGamesAsync(new GameCatalogQuery { Sort = "newest" });
        Assert.Equal("Beta Blast", newest[0].Title);
    }

    private static GameEntity MakeGame(
        int id,
        string title,
        string symbol,
        bool featured,
        GameGenreEntity[] genres,
        int createdOffsetDays) =>
        new()
        {
            Id = id,
            UserId = 1,
            Title = title,
            Symbol = symbol,
            DraftP = false,
            FeaturedP = featured,
            FeaturedOrder = featured ? 1 : null,
            Genres = genres,
            Kind = GameContentKind.Game,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-createdOffsetDays),
            Builds =
            [
                new GameBuildEntity
                {
                    Platform = GamePlatform.Windows,
                    CurrentVersion = "1.0.0",
                    ExeName = "game.exe",
                    ZipLink = "/x.zip",
                    IsDefault = true
                }
            ]
        };

    private static SgsServerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SgsServerDbContext(options);
    }
}
