using Microsoft.EntityFrameworkCore;
using SGSClient.Core.Enums;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Persistence.Services;

namespace SGSClient.Server.Tests;

public class ChatServiceTests
{
    private const int Me = 1;
    private const int Peer = 2;

    [Fact]
    public async Task Conversation_ShowsTheNewest200OldestFirst_AndMarksEveryUnreadMessageRead()
    {
        await using var db = CreateDb();
        var start = DateTimeOffset.UtcNow.AddHours(-1);
        for (var i = 0; i < 250; i++)
        {
            db.DirectMessages.Add(new DirectMessageEntity
            {
                SenderUserId = Peer,
                RecipientUserId = Me,
                Text = $"m{i}",
                CreatedAtUtc = start.AddSeconds(i)
            });
        }

        db.SaveChanges();
        var sut = new EfChatService(db, new EfNotificationService(db));

        var convo = await sut.GetConversationAsync(Me, Peer);

        Assert.Equal(200, convo.Messages.Count);
        Assert.Equal("m50", convo.Messages[0].Text);
        Assert.Equal("m249", convo.Messages[^1].Text);
        Assert.Equal(0, Assert.Single(await sut.ListThreadsAsync(Me)).UnreadCount);
    }

    private static SgsServerDbContext CreateDb()
    {
        var db = new SgsServerDbContext(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        db.Users.AddRange(
            new UserEntity { Id = Me, AzureSub = "me", DisplayName = "Me" },
            new UserEntity { Id = Peer, AzureSub = "peer", DisplayName = "Peer" });
        db.UserRelationships.Add(new UserRelationshipEntity
        {
            RequesterUserId = Me,
            AddresseeUserId = Peer,
            Kind = UserRelationshipKind.Friend,
            Status = UserRelationshipStatus.Accepted,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
        return db;
    }
}
