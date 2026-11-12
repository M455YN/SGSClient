using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Persistence.Services;

namespace SGSClient.Server.Tests;

public class LobbyServiceTests
{
    private const int Host = 5;
    private const int Friend = 10;
    private const int Stranger = 11;
    private const int GameId = 1;

    [Fact]
    public async Task HostCanCreateReadUpdateAndCloseALobby()
    {
        await using var db = CreateDb();
        var (sut, _) = CreateSut(db);

        var lobby = await sut.CreateAsync(Host, Request());

        Assert.Matches("^[A-HJ-NP-Z2-9]{6}$", lobby.Code);
        Assert.Equal("Demo", lobby.GameTitle);
        Assert.Equal("Host", lobby.HostName);

        var updated = await sut.UpdateAsync(Host, lobby.Code.ToLowerInvariant(), new UpdateLobbyRequest { Players = 3 });
        Assert.Equal(3, updated.Players);
        Assert.Equal(3, (await sut.GetAsync(lobby.Code))!.Players);

        await sut.CloseAsync(Host, lobby.Code);
        Assert.Null(await sut.GetAsync(lobby.Code));
    }

    [Fact]
    public async Task Create_RejectsUnownedGamesAndBadInput_AndReplacesTheHostsOldLobby()
    {
        await using var db = CreateDb();
        var (sut, _) = CreateSut(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.CreateAsync(Stranger, Request()));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.CreateAsync(Host, Request(gameId: 99)));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(Host, Request(address: "has space")));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(Host, Request(address: " ")));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(Host, Request(players: 5)));

        var first = await sut.CreateAsync(Host, Request());
        var second = await sut.CreateAsync(Host, Request());

        Assert.Null(await sut.GetAsync(first.Code));
        Assert.NotNull(await sut.GetAsync(second.Code));
    }

    [Fact]
    public async Task OnlyTheHostChangesALobby_AndStaleOnesExpire()
    {
        await using var db = CreateDb();
        var (sut, store) = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, Request());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.UpdateAsync(Friend, lobby.Code, new UpdateLobbyRequest { IsOpen = false }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.CloseAsync(Friend, lobby.Code));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateAsync(Host, lobby.Code, new UpdateLobbyRequest { Players = 0 }));

        store.Lobbies[lobby.Code].LastSeenUtc = DateTimeOffset.UtcNow - ILobbyService.Lifetime - TimeSpan.FromSeconds(1);

        Assert.Null(await sut.GetAsync(lobby.Code));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateAsync(Host, lobby.Code, new UpdateLobbyRequest()));
    }

    [Fact]
    public async Task Invite_NotifiesAFriendWithTheCode_AndRefusesStrangers()
    {
        await using var db = CreateDb();
        var (sut, _) = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, Request());

        var notification = await sut.InviteAsync(Host, lobby.Code, Friend);

        Assert.Equal(ILobbyService.InviteNotificationKind, notification.Kind);
        using var data = JsonDocument.Parse(notification.DataJson!);
        Assert.Equal(lobby.Code, data.RootElement.GetProperty("code").GetString());
        Assert.Equal(GameId, data.RootElement.GetProperty("gameId").GetInt32());
        Assert.Single(await db.UserNotifications.Where(n => n.UserId == Friend).ToListAsync());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.InviteAsync(Host, lobby.Code, Stranger));
    }

    [Fact]
    public async Task FriendLobbies_ListOnlyOpenLobbiesWithAFreeSlotThatFriendsHost()
    {
        await using var db = CreateDb();
        var (sut, _) = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, Request());

        Assert.Equal(lobby.Code, Assert.Single(await sut.ListFriendLobbiesAsync(Friend)).Code);
        Assert.Empty(await sut.ListFriendLobbiesAsync(Stranger));

        await sut.UpdateAsync(Host, lobby.Code, new UpdateLobbyRequest { Players = 4 });
        Assert.Empty(await sut.ListFriendLobbiesAsync(Friend));

        await sut.UpdateAsync(Host, lobby.Code, new UpdateLobbyRequest { Players = 2, IsOpen = false });
        Assert.Empty(await sut.ListFriendLobbiesAsync(Friend));
    }

    private static CreateLobbyRequest Request(int gameId = GameId, string address = "192.168.0.5:7777", int players = 1) =>
        new() { GameId = gameId, Address = address, MaxPlayers = 4, Players = players };

    private static (LobbyService Sut, LobbyStore Store) CreateSut(SgsServerDbContext db)
    {
        db.Users.AddRange(
            new UserEntity { Id = Host, AzureSub = "h", DisplayName = "Host" },
            new UserEntity { Id = Friend, AzureSub = "f", DisplayName = "Friend" },
            new UserEntity { Id = Stranger, AzureSub = "s", DisplayName = "Stranger" });
        db.Games.Add(new GameEntity { Id = GameId, UserId = Host, Title = "Demo", Symbol = "demo" });
        db.UserRelationships.Add(new UserRelationshipEntity
        {
            RequesterUserId = Host,
            AddresseeUserId = Friend,
            Kind = UserRelationshipKind.Friend,
            Status = UserRelationshipStatus.Accepted,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        var notifications = new EfNotificationService(db);
        var store = new LobbyStore();
        return (new LobbyService(db, store, new FriendsService(db, notifications), notifications), store);
    }

    private static SgsServerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
