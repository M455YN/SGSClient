using Microsoft.EntityFrameworkCore;
using SGSClient.Core.Enums;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Persistence.Services;

namespace SGSClient.Server.Tests;

public class EntitlementServiceTests
{
    [Fact]
    public async Task ClaimFreeAsync_IsIdempotent()
    {
        await using var db = CreateDb();
        SeedGame(db, id: 1, kind: GameContentKind.Game);
        var sut = new EfEntitlementService(db);

        var first = await sut.ClaimFreeAsync(userId: 10, gameId: 1);
        var second = await sut.ClaimFreeAsync(userId: 10, gameId: 1);

        Assert.Equal(1, first.Id);
        Assert.Equal(1, second.Id);
        Assert.Equal(1, await db.UserGameEntitlements.CountAsync());
    }

    [Fact]
    public async Task ClaimFreeAsync_DlcRequiresParentEntitlement()
    {
        await using var db = CreateDb();
        SeedGame(db, id: 1, kind: GameContentKind.Game);
        SeedGame(db, id: 2, kind: GameContentKind.Dlc, parentId: 1);
        var sut = new EfEntitlementService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ClaimFreeAsync(10, 2));

        await sut.ClaimFreeAsync(10, 1);
        var dlc = await sut.ClaimFreeAsync(10, 2);
        Assert.Equal(2, dlc.Id);
    }

    [Fact]
    public async Task CanDownloadPackageAsync_RequiresEntitlementUnlessOwnerOrAdmin()
    {
        await using var db = CreateDb();
        SeedGame(db, id: 1, kind: GameContentKind.Game, ownerUserId: 5, symbol: "demo");
        var sut = new EfEntitlementService(db);

        Assert.False(await sut.CanDownloadPackageAsync(10, "demo", isAdmin: false));
        Assert.True(await sut.CanDownloadPackageAsync(5, "demo", isAdmin: false));
        Assert.True(await sut.CanDownloadPackageAsync(10, "demo", isAdmin: true));

        await sut.ClaimFreeAsync(10, 1);
        Assert.True(await sut.CanDownloadPackageAsync(10, "demo", isAdmin: false));
    }

    private static SgsServerDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new SgsServerDbContext(options);
    }

    private static void SeedGame(
        SgsServerDbContext db,
        int id,
        GameContentKind kind,
        int? parentId = null,
        int ownerUserId = 1,
        string? symbol = null)
    {
        db.Games.Add(new GameEntity
        {
            Id = id,
            UserId = ownerUserId,
            Title = $"Game {id}",
            Symbol = symbol ?? $"game-{id}",
            Kind = kind,
            ParentGameId = parentId,
            DraftP = false,
            Builds =
            [
                new GameBuildEntity
                {
                    Platform = GamePlatform.Windows,
                    Architecture = "x64",
                    CurrentVersion = "1.0.0",
                    ExeName = "game.exe",
                    ZipLink = $"/api/games/packages/game-{id}/1.0.0/win.zip",
                    IsDefault = true
                }
            ]
        });
        db.SaveChanges();
    }
}
