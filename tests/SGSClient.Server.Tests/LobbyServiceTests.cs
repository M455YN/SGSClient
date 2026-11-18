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
    private const int FriendWithoutGame = 12;
    private const int SecondFriend = 13;
    private const int GameId = 1;

    [Fact]
    public async Task HostCanCreateReadUpdateAndCloseALobby()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);

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
        var sut = CreateSut(db);

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
    public async Task Create_RespectsTheGamesDeclaredLobbyMaxPlayers()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);
        (await db.Games.SingleAsync(g => g.Id == GameId)).LobbyMaxPlayers = 3;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(Host, Request()));
        Assert.Equal(3, (await sut.CreateAsync(Host, new CreateLobbyRequest { GameId = GameId, MaxPlayers = 3 })).MaxPlayers);
    }

    [Fact]
    public async Task OnlyTheHostChangesALobby_AndStaleOnesExpire()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, Request());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.UpdateAsync(Friend, lobby.Code, new UpdateLobbyRequest { IsOpen = false }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.CloseAsync(Friend, lobby.Code));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateAsync(Host, lobby.Code, new UpdateLobbyRequest { Players = 0 }));

        (await db.Lobbies.SingleAsync(l => l.Code == lobby.Code)).LastSeenUtc = DateTimeOffset.UtcNow - ILobbyService.Lifetime - TimeSpan.FromSeconds(1);
        await db.SaveChangesAsync();

        Assert.Null(await sut.GetAsync(lobby.Code));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => sut.UpdateAsync(Host, lobby.Code, new UpdateLobbyRequest()));
    }

    [Fact]
    public async Task Invite_NotifiesAFriendWithTheCode_AndRefusesStrangers()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);
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
        var sut = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, Request());

        Assert.Equal(lobby.Code, Assert.Single(await sut.ListFriendLobbiesAsync(Friend)).Code);
        Assert.Empty(await sut.ListFriendLobbiesAsync(Stranger));

        await sut.UpdateAsync(Host, lobby.Code, new UpdateLobbyRequest { Players = 4 });
        Assert.Empty(await sut.ListFriendLobbiesAsync(Friend));

        await sut.UpdateAsync(Host, lobby.Code, new UpdateLobbyRequest { Players = 2, IsOpen = false });
        Assert.Empty(await sut.ListFriendLobbiesAsync(Friend));
    }

    [Fact]
    public async Task Join_AddsAFriendWhoOwnsTheGame_AndRefusesOthers()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, new CreateLobbyRequest { GameId = GameId, MaxPlayers = 2 });
        Assert.Equal(string.Empty, lobby.Address);

        var joined = await sut.JoinAsync(Friend, lobby.Code);

        Assert.Equal([Host, Friend], joined.Members.Select(m => m.UserId));
        Assert.Equal("Friend", joined.Members[1].DisplayName);
        Assert.Equal(2, joined.Players);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.JoinAsync(Stranger, lobby.Code));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.JoinAsync(FriendWithoutGame, lobby.Code));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.JoinAsync(SecondFriend, lobby.Code));
    }

    [Fact]
    public async Task Start_IsHostOnly_AndHidesTheLobby_AndHostLeavingClosesIt()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, new CreateLobbyRequest { GameId = GameId });
        await sut.JoinAsync(Friend, lobby.Code);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.StartAsync(Friend, lobby.Code));
        Assert.Single(await sut.ListFriendLobbiesAsync(SecondFriend));

        var started = await sut.StartAsync(Host, lobby.Code);
        Assert.True(started.Started);
        Assert.False(started.IsOpen);
        Assert.Empty(await sut.ListFriendLobbiesAsync(SecondFriend));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.JoinAsync(SecondFriend, lobby.Code));

        Assert.Equal(1, (await sut.LeaveAsync(Friend, lobby.Code))!.Players);
        Assert.Null(await sut.LeaveAsync(Host, lobby.Code));
        Assert.Null(await sut.GetAsync(lobby.Code));
    }

    [Fact]
    public async Task Invite_RefusesAFriendWithoutTheGame_AndInvitableListsOnlyOwnersNotInTheLobby()
    {
        await using var db = CreateDb();
        var sut = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, new CreateLobbyRequest { GameId = GameId });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.InviteAsync(Host, lobby.Code, FriendWithoutGame));
        Assert.Equal([Friend, SecondFriend], (await sut.ListInvitableAsync(Host, lobby.Code)).Select(f => f.UserId).Order());

        await sut.JoinAsync(Friend, lobby.Code);
        Assert.Equal([SecondFriend], (await sut.ListInvitableAsync(Host, lobby.Code)).Select(f => f.UserId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.ListInvitableAsync(Friend, lobby.Code));
    }

    [Fact]
    public async Task Lobbies_SurviveANewServiceInstance_AndTheLastSlotGoesToOnePlayer()
    {
        var name = Guid.NewGuid().ToString("N");
        await using var db = CreateDb(name);
        var sut = CreateSut(db);
        var lobby = await sut.CreateAsync(Host, new CreateLobbyRequest { GameId = GameId, MaxPlayers = 2 });

        // A second API instance (or one after a restart) sees the same lobby.
        await using var otherDb = CreateDb(name);
        var other = new LobbyService(otherDb, new FriendsService(otherDb, new EfNotificationService(otherDb)), new EfNotificationService(otherDb));
        Assert.Equal(Host, (await other.GetAsync(lobby.Code))!.HostUserId);

        // Both instances read the lobby with one free slot; only the first join may take it.
        await other.GetAsync(lobby.Code);
        await sut.JoinAsync(Friend, lobby.Code);
        await Assert.ThrowsAsync<ArgumentException>(() => other.JoinAsync(SecondFriend, lobby.Code));
        Assert.Equal([Host, Friend], (await sut.GetAsync(lobby.Code))!.Members.Select(m => m.UserId));
    }

    private static CreateLobbyRequest Request(int gameId = GameId, string address = "192.168.0.5:7777", int players = 1) =>
        new() { GameId = gameId, Address = address, MaxPlayers = 4, Players = players };

    private static LobbyService CreateSut(SgsServerDbContext db)
    {
        db.Users.AddRange(
            new UserEntity { Id = Host, AzureSub = "h", DisplayName = "Host" },
            new UserEntity { Id = Friend, AzureSub = "f", DisplayName = "Friend" },
            new UserEntity { Id = Stranger, AzureSub = "s", DisplayName = "Stranger" },
            new UserEntity { Id = FriendWithoutGame, AzureSub = "n", DisplayName = "NoGame" },
            new UserEntity { Id = SecondFriend, AzureSub = "t", DisplayName = "Second" });
        db.Games.Add(new GameEntity { Id = GameId, UserId = Host, Title = "Demo", Symbol = "demo" });
        foreach (var id in new[] { Friend, FriendWithoutGame, SecondFriend })
        {
            db.UserRelationships.Add(new UserRelationshipEntity
            {
                RequesterUserId = Host,
                AddresseeUserId = id,
                Kind = UserRelationshipKind.Friend,
                Status = UserRelationshipStatus.Accepted,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        db.UserGameEntitlements.AddRange(
            new UserGameEntitlementEntity { UserId = Friend, GameId = GameId, GrantedAtUtc = DateTimeOffset.UtcNow },
            new UserGameEntitlementEntity { UserId = SecondFriend, GameId = GameId, GrantedAtUtc = DateTimeOffset.UtcNow });
        db.SaveChanges();

        var notifications = new EfNotificationService(db);
        return new LobbyService(db, new FriendsService(db, notifications), notifications);
    }

    private static SgsServerDbContext CreateDb(string? name = null) =>
        new(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString("N"))
            .Options);
}
