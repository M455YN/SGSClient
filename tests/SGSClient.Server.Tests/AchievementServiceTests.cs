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

public class AchievementServiceTests
{
    private const int Owner = 5;
    private const int Player = 10;
    private const int GameId = 1;

    [Fact]
    public async Task Sync_UpsertsInRequestOrder_DeactivatesMissingKeysAndSetsFeatureFlag()
    {
        await using var db = CreateDb();
        SeedGame(db);
        var sut = CreateSut(db);

        await sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("a", "A"), Def("b", "B"), Def("c", "C")));
        var result = await sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("c", "C2"), Def("a", "A")));

        Assert.Equal(["c", "a"], result.Select(r => r.Key));
        Assert.Equal("C2", result[0].Title);

        var b = await db.GameAchievements.SingleAsync(x => x.Key == "b");
        Assert.False(b.IsActive);
        Assert.True((await db.Games.SingleAsync()).Features.HasFlag(GameFeatures.Achievements));

        await sut.SyncDefinitionsAsync(GameId, Owner, Request());
        Assert.False((await db.Games.SingleAsync()).Features.HasFlag(GameFeatures.Achievements));
    }

    [Fact]
    public async Task Sync_ReactivatingAKeyKeepsPlayerProgress()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("boss", "Boss")));
        await sut.UnlockAsync(Player, GameId, "boss");

        await sut.SyncDefinitionsAsync(GameId, Owner, Request());
        await sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("boss", "Boss")));

        var view = await sut.GetForGameAsync(GameId, Player);
        Assert.NotNull(view!.Items.Single().UnlockedAtUtc);
    }

    [Fact]
    public async Task Sync_RejectsInvalidInputAndForeignOwner()
    {
        await using var db = CreateDb();
        SeedGame(db);
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("a", "A"), Def("A ", "Again"))));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("bad key!", "X"))));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("a", "A", target: 1))));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.SyncDefinitionsAsync(GameId, Player, Request(Def("a", "A"))));
        Assert.Empty(await db.GameAchievements.ToListAsync());
    }

    [Fact]
    public async Task Unlock_IsIdempotent_ReportsJustUnlockedOnce_AndNotifiesOnce()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("boss", "Boss")));

        var first = await sut.UnlockAsync(Player, GameId, "  BOSS ");
        var second = await sut.UnlockAsync(Player, GameId, "boss");

        Assert.True(first.JustUnlocked);
        Assert.True(first.Unlocked);
        Assert.False(second.JustUnlocked);
        Assert.True(second.Unlocked);
        Assert.Equal(1, await db.UserGameAchievements.CountAsync());
        Assert.Equal(1, await db.UserNotifications.CountAsync(n => n.UserId == Player));
    }

    [Fact]
    public async Task Unlock_RequiresLibraryEntryUnlessOwner_AndKnownKey()
    {
        await using var db = CreateDb();
        SeedGame(db);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("boss", "Boss")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.UnlockAsync(Player, GameId, "boss"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UnlockAsync(Owner, GameId, "nope"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UnlockAsync(Owner, 999, "boss"));
        Assert.True((await sut.UnlockAsync(Owner, GameId, "boss")).JustUnlocked);
    }

    [Fact]
    public async Task Progress_UnlocksAtTarget_NeverMovesBackwards_AndIsClamped()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(
            GameId,
            Owner,
            Request(Def("kills", "Kills", target: 10), Def("once", "Once")));

        var a = await sut.SetProgressAsync(Player, GameId, "kills", 4);
        var b = await sut.SetProgressAsync(Player, GameId, "kills", 2);
        var c = await sut.SetProgressAsync(Player, GameId, "kills", 500);
        var d = await sut.SetProgressAsync(Player, GameId, "kills", 10);

        Assert.Equal(4, a.Progress);
        Assert.False(a.Unlocked);
        Assert.Equal(4, b.Progress);
        Assert.Equal(10, c.Progress);
        Assert.True(c.JustUnlocked);
        Assert.False(d.JustUnlocked);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetProgressAsync(Player, GameId, "once", 1));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetProgressAsync(Player, GameId, "kills", -1));
    }

    [Fact]
    public async Task GetForGame_MasksHiddenUntilUnlocked_AndComputesRarity()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player, 11, 12, 13]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(
            GameId,
            Owner,
            Request(Def("secret", "Secret", hidden: true, points: 50), Def("easy", "Easy", points: 5)));
        await sut.UnlockAsync(Player, GameId, "secret");
        await sut.UnlockAsync(Player, GameId, "easy");
        await sut.UnlockAsync(11, GameId, "easy");

        var mine = await sut.GetForGameAsync(GameId, Player);
        var other = await sut.GetForGameAsync(GameId, 12);

        var secretMine = mine!.Items.Single(i => i.Key == "secret");
        var secretOther = other!.Items.Single(i => i.Key == "secret");
        Assert.Equal("Secret", secretMine.Title);
        Assert.Equal(string.Empty, secretOther.Title);
        Assert.Null(secretOther.IconUrl);
        Assert.Equal(25d, secretMine.RarityPercent);
        Assert.Equal(50d, mine.Items.Single(i => i.Key == "easy").RarityPercent);
        Assert.Equal(2, mine.Unlocked);
        Assert.Equal(55, mine.PointsEarned);
        Assert.Equal(55, mine.PointsTotal);
        Assert.Equal(0, other.Unlocked);
    }

    [Fact]
    public async Task GetForGame_HidesDraftsFromEveryoneButTheOwner()
    {
        await using var db = CreateDb();
        SeedGame(db, draft: true);
        var sut = CreateSut(db);

        Assert.Null(await sut.GetForGameAsync(GameId, Player));
        Assert.Null(await sut.GetForGameAsync(GameId, viewerUserId: null));
        Assert.NotNull(await sut.GetForGameAsync(GameId, Owner));
    }

    [Fact]
    public async Task ResetProgress_ClearsOnlyTheOwnersRows_AndSummariesCountUnlocks()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(
            GameId,
            Owner,
            Request(Def("a", "A", points: 10), Def("b", "B", points: 20)));
        await sut.UnlockAsync(Owner, GameId, "a");
        await sut.UnlockAsync(Player, GameId, "a");
        await sut.UnlockAsync(Player, GameId, "b");

        var summary = Assert.Single(await sut.GetSummariesAsync(Player));
        Assert.Equal((GameId, 2, 2, 30), (summary.GameId, summary.Unlocked, summary.Total, summary.PointsEarned));

        await sut.ResetProgressAsync(GameId, Owner);

        Assert.Empty(await sut.GetSummariesAsync(Owner));
        Assert.Equal(2, await db.UserGameAchievements.CountAsync(u => u.UserId == Player));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.ResetProgressAsync(GameId, Player));
    }

    [Fact]
    public async Task GetForGame_UsesTranslationForTheRequestedLanguage_AndFallsBackToBaseText()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, Request(new AchievementDefinitionDto
        {
            Key = "boss",
            Title = "Pogromca",
            Description = "Pokonaj bossa",
            Translations = new Dictionary<string, AchievementTextDto>
            {
                ["EN"] = new() { Title = "Slayer", Description = "Defeat the boss" },
                ["de"] = new() { Title = "  ", Description = "  " }
            }
        }));

        var english = (await sut.GetForGameAsync(GameId, Player, "en-US"))!.Items.Single();
        var german = (await sut.GetForGameAsync(GameId, Player, "de"))!.Items.Single();
        var none = (await sut.GetForGameAsync(GameId, Player))!.Items.Single();

        Assert.Equal(("Slayer", "Defeat the boss"), (english.Title, english.Description));
        Assert.Equal("Pogromca", german.Title);
        Assert.Equal("Pogromca", none.Title);

        var stored = Assert.Single(await sut.GetDefinitionsAsync(GameId, Owner));
        Assert.Equal(["en"], stored.Translations!.Keys);
    }

    [Fact]
    public async Task Sync_RejectsBadLanguageCodes()
    {
        await using var db = CreateDb();
        SeedGame(db);
        var sut = CreateSut(db);

        await Assert.ThrowsAsync<ArgumentException>(() => sut.SyncDefinitionsAsync(GameId, Owner, Request(
            new AchievementDefinitionDto
            {
                Key = "a",
                Title = "A",
                Translations = new Dictionary<string, AchievementTextDto> { ["english!"] = new() { Title = "x" } }
            })));
    }

    [Fact]
    public async Task Admin_CanReadAndEditAForeignGame_ButAPlayerCannot()
    {
        await using var db = CreateDb();
        SeedGame(db);
        var sut = CreateSut(db);
        const int admin = 77;

        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.GetDefinitionsAsync(GameId, admin));
        var saved = await sut.SyncDefinitionsAsync(GameId, admin, Request(Def("a", "A")), isAdmin: true);

        Assert.Single(saved);
        Assert.Single(await sut.GetDefinitionsAsync(GameId, admin, isAdmin: true));
        await sut.ResetProgressAsync(GameId, admin, isAdmin: true);
    }

    [Fact]
    public async Task SetIcon_StoresTheFileAndPointsTheDefinitionAtIt()
    {
        var storage = Path.Combine(Path.GetTempPath(), $"sgs-icon-{Guid.NewGuid():N}");
        try
        {
            await using var db = CreateDb();
            SeedGame(db);
            var sut = CreateSut(db, storage);
            await sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("boss", "Boss")));

            await using var png = new MemoryStream([1, 2, 3, 4]);
            var updated = await sut.SetIconAsync(GameId, Owner, "boss", png, "icon.PNG");

            Assert.Equal("/api/games/media/demo/achievement_boss.png", updated.IconUrl);
            Assert.True(File.Exists(Path.Combine(storage, "demo", "media", "achievement_boss.png")));

            await using var exe = new MemoryStream([1]);
            await Assert.ThrowsAsync<ArgumentException>(() => sut.SetIconAsync(GameId, Owner, "boss", exe, "icon.exe"));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetIconAsync(GameId, Owner, "nope", png, "icon.png"));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.SetIconAsync(GameId, Player, "boss", png, "icon.png"));

            // The relative path survives a round trip through the panel (it is re-sent on save).
            var resaved = await sut.SyncDefinitionsAsync(GameId, Owner, Request(
                new AchievementDefinitionDto { Key = "boss", Title = "Boss", IconUrl = updated.IconUrl }));
            Assert.Equal(updated.IconUrl, resaved.Single().IconUrl);
        }
        finally
        {
            if (Directory.Exists(storage))
            {
                Directory.Delete(storage, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Summaries_CarryTheGameTitle()
    {
        await using var db = CreateDb();
        SeedGame(db, entitled: [Player]);
        var sut = CreateSut(db);
        await sut.SyncDefinitionsAsync(GameId, Owner, Request(Def("a", "A")));
        await sut.UnlockAsync(Player, GameId, "a");

        Assert.Equal("Demo", Assert.Single(await sut.GetSummariesAsync(Player)).GameTitle);
    }

    private static EfAchievementService CreateSut(SgsServerDbContext db, string? storagePath = null) =>
        new(
            db,
            new EfNotificationService(db),
            new GameMediaService(
                db,
                Options.Create(new GameStorageSettings
                {
                    StoragePath = storagePath ?? Path.Combine(Path.GetTempPath(), "sgs-ach-tests")
                })));

    private static SyncGameAchievementsRequest Request(params AchievementDefinitionDto[] items) =>
        new() { Items = items };

    private static AchievementDefinitionDto Def(
        string key,
        string title,
        int points = 10,
        int? target = null,
        bool hidden = false) =>
        new() { Key = key, Title = title, Points = points, Target = target, Hidden = hidden };

    private static SgsServerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SgsServerDbContext(options);
    }

    private static void SeedGame(SgsServerDbContext db, bool draft = false, int[]? entitled = null)
    {
        db.Games.Add(new GameEntity
        {
            Id = GameId,
            UserId = Owner,
            Title = "Demo",
            Symbol = "demo",
            DraftP = draft
        });
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
