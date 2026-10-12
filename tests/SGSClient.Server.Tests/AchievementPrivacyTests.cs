using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Persistence.Services;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Server.Tests;

public class AchievementPrivacyTests
{
    private const int Creator = 5;
    private const int Player = 10;
    private const int Stranger = 20;

    [Fact]
    public async Task PublicProfile_ShowsAchievementsByDefault()
    {
        await using var db = await SeedAsync();
        var sut = CreateProfiles(db);

        var profile = await sut.GetPublicProfileAsync(Player, Stranger);

        Assert.False(profile.AchievementsHidden);
        var line = Assert.Single(profile.Achievements);
        Assert.Equal(("Demo", 1, 2), (line.GameTitle, line.Unlocked, line.Total));
    }

    [Fact]
    public async Task PublicProfile_HiddenAchievementsAreEmptyForOthersButVisibleToTheOwner()
    {
        await using var db = await SeedAsync();
        var sut = CreateProfiles(db);
        var users = CreateUsers(db);
        var updated = await users.UpdateProfileAsync("sub-player", new UpdateProfileRequestDto { HideAchievements = true });
        Assert.True(updated!.HideAchievements);

        var asStranger = await sut.GetPublicProfileAsync(Player, Stranger);
        var asAnonymous = await sut.GetPublicProfileAsync(Player, viewerUserId: null);
        var asOwner = await sut.GetPublicProfileAsync(Player, Player);
        var byTag = await sut.GetPublicProfileByTagAsync("player", Stranger);

        Assert.True(asStranger.AchievementsHidden);
        Assert.Empty(asStranger.Achievements);
        Assert.Empty(asAnonymous.Achievements);
        Assert.Empty(byTag!.Achievements);
        Assert.True(asOwner.AchievementsHidden);
        Assert.Single(asOwner.Achievements);
    }

    [Fact]
    public async Task UpdateProfile_LeavesThePrivacySettingAloneWhenNotSent_AndCanTurnItBackOff()
    {
        await using var db = await SeedAsync();
        var users = CreateUsers(db);

        await users.UpdateProfileAsync("sub-player", new UpdateProfileRequestDto { HideAchievements = true });
        var afterBio = await users.UpdateProfileAsync("sub-player", new UpdateProfileRequestDto { Bio = "hi" });
        var afterOff = await users.UpdateProfileAsync("sub-player", new UpdateProfileRequestDto { HideAchievements = false });

        Assert.True(afterBio!.HideAchievements);
        Assert.False(afterOff!.HideAchievements);
    }

    [Fact]
    public async Task OwnProfile_ListsLibraryGamesWithNoUnlocksOthersDoNot()
    {
        await using var db = await SeedAsync();
        db.UserGameEntitlements.Add(new UserGameEntitlementEntity { UserId = Stranger, GameId = 1, GrantedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var sut = CreateProfiles(db);

        var own = await sut.GetPublicProfileAsync(Stranger, Stranger);
        var other = await sut.GetPublicProfileAsync(Stranger, Player);

        var line = Assert.Single(own.Achievements);
        Assert.Equal((0, 2), (line.Unlocked, line.Total));
        Assert.Empty(other.Achievements);
    }

    private static EfProfileCommentService CreateProfiles(SgsServerDbContext db) =>
        new(db, new EfAchievementService(
            db,
            new EfNotificationService(db),
            new GameMediaService(db, Options.Create(new GameStorageSettings { StoragePath = Path.GetTempPath() }))));

    private static EfUserService CreateUsers(SgsServerDbContext db) =>
        new(db, new AdminAuthorization(Options.Create(new AdminSettings()), Options.Create(new AzureAdSettings())));

    /// <summary>A game with two achievements, of which the player has unlocked one.</summary>
    private static async Task<SgsServerDbContext> SeedAsync()
    {
        var db = new SgsServerDbContext(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        db.Users.AddRange(
            new UserEntity { Id = Creator, AzureSub = "sub-creator", DisplayName = "Creator" },
            new UserEntity { Id = Player, AzureSub = "sub-player", DisplayName = "Player", PlayerTag = "player" },
            new UserEntity { Id = Stranger, AzureSub = "sub-stranger", DisplayName = "Stranger" });
        db.Games.Add(new GameEntity { Id = 1, UserId = Creator, Title = "Demo", Symbol = "demo" });
        db.UserGameEntitlements.Add(new UserGameEntitlementEntity
        {
            UserId = Player,
            GameId = 1,
            GrantedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var achievements = new EfAchievementService(
            db,
            new EfNotificationService(db),
            new GameMediaService(db, Options.Create(new GameStorageSettings { StoragePath = Path.GetTempPath() })));
        await achievements.SyncDefinitionsAsync(
            1,
            Creator,
            new SyncGameAchievementsRequest
            {
                Items =
                [
                    new AchievementDefinitionDto { Key = "a", Title = "A", Points = 10 },
                    new AchievementDefinitionDto { Key = "b", Title = "B", Points = 20 }
                ]
            });
        await achievements.UnlockAsync(Player, 1, "a");
        return db;
    }
}
