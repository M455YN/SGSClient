using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Persistence.Services;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Server.Tests;

/// <summary>Steamworks-style stats: definitions, stat-threshold unlocks, locked icons and testing resets.</summary>
public class AchievementStatsServiceTests
{
    private const int Owner = 5;
    private const int Player = 10;
    private const int GameId = 1;

    [Fact]
    public async Task SetStat_MovesLinkedProgress_AndUnlocksOnceTheThresholdIsReached()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, WinsSetup());

        var below = await sut.SetStatAsync(Player, GameId, "NUM_GAMES", 7);
        var progress = Assert.Single(below.Achievements);
        Assert.Equal(("ach_win_10", 7, false), (progress.Key, progress.Progress, progress.Unlocked));

        var reached = await sut.SetStatAsync(Player, GameId, "num_games", 12);
        var unlocked = Assert.Single(reached.Achievements);
        Assert.True(unlocked.JustUnlocked);
        Assert.Equal(10, unlocked.Progress);

        var view = await sut.GetForGameAsync(GameId, Player);
        Assert.NotNull(view!.Items.Single(i => i.Key == "ach_win_10").UnlockedAtUtc);
        Assert.Null(view.Items.Single(i => i.Key == "ach_first").UnlockedAtUtc); // not linked to the stat
        Assert.Equal(12d, view.Stats.Single(s => s.Key == "num_games").Value);
        Assert.Equal(1, await db.UserNotifications.CountAsync(n => n.UserId == Player));
    }

    [Fact]
    public async Task SetStat_RejectsWritesThatBreakTheDefinition_AndDirectProgressOnALinkedAchievement()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, WinsSetup());
        await sut.SetStatAsync(Player, GameId, "num_games", 5);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetStatAsync(Player, GameId, "num_games", 4)); // increment only
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetStatAsync(Player, GameId, "num_games", 5.5)); // INT
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetStatAsync(Player, GameId, "nope", 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.SetStatAsync(99, GameId, "num_games", 6));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetProgressAsync(Player, GameId, "ach_win_10", 10));
        Assert.Equal(5d, (await db.UserGameStats.SingleAsync()).Value);
    }

    [Fact]
    public async Task Sync_RejectsALinkToAMissingStat_AndKeepsStatsWhenTheRequestOmitsThem()
    {
        await using var db = CreateDb();
        SeedGame(db);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, WinsSetup());

        // Stats omitted: the stored ones stay and still satisfy the link.
        await sut.SyncDefinitionsAsync(GameId, Owner, new SyncGameAchievementsRequest { Items = WinsSetup().Items });
        Assert.Single(await sut.GetStatDefinitionsAsync(GameId, Owner));

        // Removing a stat that an achievement still uses fails and saves nothing.
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SyncDefinitionsAsync(
            GameId,
            Owner,
            new SyncGameAchievementsRequest { Items = WinsSetup().Items, Stats = [] }));
        Assert.True((await db.GameStats.AsNoTracking().SingleAsync()).IsActive);
    }

    [Fact]
    public async Task GetForGame_MasksHiddenLockedIconsToo_AndShowsThemOnceUnlocked()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, new SyncGameAchievementsRequest
        {
            Items =
            [
                new() { Key = "secret", Title = "Secret", Description = "Shh", Hidden = true, IconUrl = "/a.png", LockedIconUrl = "/a_gray.png" },
                new() { Key = "open", Title = "Open", IconUrl = "/b.png", LockedIconUrl = "/b_gray.png" }
            ]
        });

        var before = await sut.GetForGameAsync(GameId, Player);
        var secret = before!.Items.Single(i => i.Key == "secret");
        Assert.Equal((string.Empty, string.Empty, (string?)null, (string?)null), (secret.Title, secret.Description, secret.IconUrl, secret.LockedIconUrl));
        Assert.Equal("/b_gray.png", before.Items.Single(i => i.Key == "open").LockedIconUrl);

        await sut.UnlockAsync(Player, GameId, "secret");
        var after = (await sut.GetForGameAsync(GameId, Player))!.Items.Single(i => i.Key == "secret");
        Assert.Equal(("Secret", "Shh", "/a.png"), (after.Title, after.Description, after.IconUrl));
    }

    [Fact]
    public async Task ResetProgress_ClearsTheCallersStatsToo()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, WinsSetup());
        await sut.SetStatAsync(Owner, GameId, "num_games", 10);
        await sut.SetStatAsync(Player, GameId, "num_games", 3);

        await sut.ResetProgressAsync(GameId, Owner);

        Assert.Equal(Player, (await db.UserGameStats.SingleAsync()).UserId);
        Assert.Equal(0d, (await sut.GetForGameAsync(GameId, Owner))!.Stats.Single().Value);
        Assert.Equal(0, await db.UserGameAchievements.CountAsync(u => u.UserId == Owner));
    }

    private static SyncGameAchievementsRequest WinsSetup() => new()
    {
        Items =
        [
            new() { Key = "ACH_FIRST", Title = "First" },
            new() { Key = "ACH_WIN_10", Title = "Ten wins", StatKey = "NUM_GAMES", Target = 10 }
        ],
        Stats =
        [
            new() { Key = "NUM_GAMES", DisplayName = "Games won", Type = GameStatType.Int, MinValue = 0, IncrementOnly = true }
        ]
    };

    private static EfAchievementService CreateSut(SgsServerDbContext db) =>
        new(
            db,
            new EfNotificationService(db),
            new GameMediaService(
                db,
                Options.Create(new GameStorageSettings { StoragePath = Path.Combine(Path.GetTempPath(), "sgs-ach-tests") })));

    private static SgsServerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static void SeedGame(SgsServerDbContext db, int[]? entitled = null)
    {
        db.Games.Add(new GameEntity { Id = GameId, UserId = Owner, Title = "Demo", Symbol = "demo" });
        foreach (var userId in entitled ?? [])
        {
            db.UserGameEntitlements.Add(new UserGameEntitlementEntity
            {
                UserId = userId,
                GameId = GameId,
                GrantedAtUtc = DateTimeOffset.UtcNow
            });
        }

        db.SaveChanges();
    }
}
