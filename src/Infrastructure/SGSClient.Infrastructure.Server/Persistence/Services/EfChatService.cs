using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfChatService(SgsServerDbContext db, INotificationService notifications) : IChatService
{
    private const int MaxMessageLength = 2000;

    public async Task<IReadOnlyList<ChatThreadDto>> ListThreadsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var friendIds = await GetAcceptedFriendIdsAsync(userId, cancellationToken);
        if (friendIds.Count == 0)
        {
            return [];
        }

        var messages = await db.DirectMessages.AsNoTracking()
            .Where(m => (m.SenderUserId == userId && friendIds.Contains(m.RecipientUserId))
                        || (m.RecipientUserId == userId && friendIds.Contains(m.SenderUserId)))
            .OrderByDescending(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var peers = await db.Users.AsNoTracking()
            .Where(u => friendIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var threads = new List<ChatThreadDto>();
        foreach (var peerId in friendIds)
        {
            if (!peers.TryGetValue(peerId, out var peer))
            {
                continue;
            }

            var last = messages.FirstOrDefault(m =>
                (m.SenderUserId == userId && m.RecipientUserId == peerId)
                || (m.SenderUserId == peerId && m.RecipientUserId == userId));

            var unread = messages.Count(m =>
                m.SenderUserId == peerId
                && m.RecipientUserId == userId
                && m.ReadAtUtc is null);

            threads.Add(new ChatThreadDto
            {
                Peer = MapPeer(peer),
                LastMessagePreview = last is null
                    ? null
                    : (last.Text.Length <= 80 ? last.Text : last.Text[..80] + "…"),
                LastMessageAtUtc = last?.CreatedAtUtc,
                UnreadCount = unread
            });
        }

        return threads
            .OrderByDescending(t => t.LastMessageAtUtc ?? DateTimeOffset.MinValue)
            .ThenBy(t => t.Peer.DisplayName)
            .ToList();
    }

    public async Task<ChatConversationDto> GetConversationAsync(
        int userId,
        int peerUserId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFriendsAsync(userId, peerUserId, cancellationToken);

        var peer = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == peerUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono użytkownika.");

        // Newest 200, shown oldest first.
        var rows = await db.DirectMessages
            .Where(m => (m.SenderUserId == userId && m.RecipientUserId == peerUserId)
                        || (m.SenderUserId == peerUserId && m.RecipientUserId == userId))
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Take(200)
            .ToListAsync(cancellationToken);
        rows.Reverse();

        // Also older ones past the 200 shown, or the thread's unread count never drops to zero.
        var now = DateTimeOffset.UtcNow;
        var unread = await db.DirectMessages
            .Where(m => m.SenderUserId == peerUserId && m.RecipientUserId == userId && m.ReadAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var m in unread)
        {
            m.ReadAtUtc = now;
        }

        if (unread.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ChatConversationDto
        {
            Peer = MapPeer(peer),
            Messages = rows.Select(m => MapMessage(m, userId)).ToList()
        };
    }

    public async Task<ChatMessageDto> SendAsync(
        int senderUserId,
        int recipientUserId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var body = (text ?? string.Empty).Trim();
        if (body.Length is < 1 or > MaxMessageLength)
        {
            throw new ArgumentException($"Wiadomość musi mieć od 1 do {MaxMessageLength} znaków.");
        }

        await EnsureFriendsAsync(senderUserId, recipientUserId, cancellationToken);

        var entity = new DirectMessageEntity
        {
            SenderUserId = senderUserId,
            RecipientUserId = recipientUserId,
            Text = body,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.DirectMessages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var sender = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == senderUserId, cancellationToken);
        var senderName = sender?.DisplayName ?? "Znajomy";
        var preview = body.Length <= 80 ? body : body[..80] + "…";
        await notifications.CreateAsync(
            recipientUserId,
            "chat_message",
            $"Wiadomość od {senderName}",
            preview,
            $"{{\"fromUserId\":{senderUserId},\"messageId\":{entity.Id}}}",
            cancellationToken);

        return MapMessage(entity, senderUserId);
    }

    private async Task EnsureFriendsAsync(int a, int b, CancellationToken cancellationToken)
    {
        if (a == b)
        {
            throw new ArgumentException("Nie możesz napisać do siebie.");
        }

        var ok = await db.UserRelationships.AsNoTracking().AnyAsync(
            r => r.Kind == UserRelationshipKind.Friend
                 && r.Status == UserRelationshipStatus.Accepted
                 && ((r.RequesterUserId == a && r.AddresseeUserId == b)
                     || (r.RequesterUserId == b && r.AddresseeUserId == a)),
            cancellationToken);

        if (!ok)
        {
            throw new UnauthorizedAccessException("Czat jest dostępny tylko między znajomymi.");
        }
    }

    private async Task<List<int>> GetAcceptedFriendIdsAsync(int userId, CancellationToken cancellationToken)
    {
        var rows = await db.UserRelationships.AsNoTracking()
            .Where(r => r.Kind == UserRelationshipKind.Friend
                        && r.Status == UserRelationshipStatus.Accepted
                        && (r.RequesterUserId == userId || r.AddresseeUserId == userId))
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => r.RequesterUserId == userId ? r.AddresseeUserId : r.RequesterUserId)
            .Distinct()
            .ToList();
    }

    private static SocialUserDto MapPeer(UserEntity user) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        AvatarUrl = user.AvatarUrl,
        Location = user.Location,
        Bio = user.Bio,
        PlayerTag = user.PlayerTag,
        LastLoginUtc = user.LastLoginUtc,
        LastSeenUtc = user.LastSeenUtc,
        IsOnline = user.LastSeenUtc is { } seen
            && DateTimeOffset.UtcNow - seen <= FriendsService.OnlineWindow,
        FriendRelation = FriendRelation.Friends
    };

    private static ChatMessageDto MapMessage(DirectMessageEntity m, int viewerId) => new()
    {
        Id = m.Id,
        SenderUserId = m.SenderUserId,
        RecipientUserId = m.RecipientUserId,
        Text = m.Text,
        CreatedAtUtc = m.CreatedAtUtc,
        IsMine = m.SenderUserId == viewerId,
        IsRead = m.ReadAtUtc is not null
    };
}
