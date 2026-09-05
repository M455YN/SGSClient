using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class FriendsService(SgsServerDbContext db) : IFriendsService
{
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);
    public Task<SocialOverviewDto> GetOverviewAsync(int userId, CancellationToken cancellationToken = default) =>
        BuildOverviewAsync(userId, cancellationToken);

    public async Task<IReadOnlyList<SocialUserDto>> SearchUsersAsync(
        int userId,
        string query,
        CancellationToken cancellationToken = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 2)
        {
            throw new ArgumentException("Wpisz co najmniej 2 znaki.");
        }

        var normalizedQuery = q.TrimStart('@').ToLowerInvariant();

        var matches = await db.Users.AsNoTracking()
            .Where(u => u.Id != userId && (
                u.DisplayName.Contains(q)
                || (u.PlayerTag != null && u.PlayerTag.Contains(normalizedQuery))))
            .OrderBy(u => u.DisplayName)
            .Take(20)
            .ToListAsync(cancellationToken);

        var relations = await LoadRelationsAsync(userId, cancellationToken);
        return matches.Select(u => Map(u, userId, relations)).ToList();
    }

    public async Task<SocialOverviewDto> SendFriendRequestAsync(
        int userId,
        int targetUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(userId, targetUserId);
        await EnsureUserExistsAsync(targetUserId, cancellationToken);

        var existing = await FindFriendAsync(userId, targetUserId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Status == UserRelationshipStatus.Accepted)
            {
                throw new ArgumentException("Jesteście już znajomymi.");
            }

            if (existing.Status == UserRelationshipStatus.Blocked)
            {
                throw new ArgumentException("Nie można wysłać zaproszenia.");
            }

            if (existing.AddresseeUserId == userId && existing.Status == UserRelationshipStatus.Pending)
            {
                existing.Status = UserRelationshipStatus.Accepted;
                existing.RespondedAtUtc = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return await BuildOverviewAsync(userId, cancellationToken);
            }

            throw new ArgumentException("Zaproszenie już czeka na odpowiedź.");
        }

        db.UserRelationships.Add(new UserRelationshipEntity
        {
            RequesterUserId = userId,
            AddresseeUserId = targetUserId,
            Kind = UserRelationshipKind.Friend,
            Status = UserRelationshipStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return await BuildOverviewAsync(userId, cancellationToken);
    }

    public async Task<SocialOverviewDto> AcceptFriendRequestAsync(
        int userId,
        int fromUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(userId, fromUserId);

        var existing = await db.UserRelationships.FirstOrDefaultAsync(
            r => r.Kind == UserRelationshipKind.Friend
                 && r.Status == UserRelationshipStatus.Pending
                 && r.RequesterUserId == fromUserId
                 && r.AddresseeUserId == userId,
            cancellationToken);
        if (existing is null)
        {
            throw new ArgumentException("Brak zaproszenia do zaakceptowania.");
        }

        existing.Status = UserRelationshipStatus.Accepted;
        existing.RespondedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await BuildOverviewAsync(userId, cancellationToken);
    }

    public async Task<SocialOverviewDto> RemoveFriendAsync(
        int userId,
        int otherUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(userId, otherUserId);

        var existing = await FindFriendAsync(userId, otherUserId, cancellationToken);
        if (existing is null || existing.Status == UserRelationshipStatus.Blocked)
        {
            throw new ArgumentException("Nie ma takiej relacji znajomych.");
        }

        db.UserRelationships.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildOverviewAsync(userId, cancellationToken);
    }

    public async Task<SocialOverviewDto> FollowAsync(
        int userId,
        int targetUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(userId, targetUserId);
        await EnsureUserExistsAsync(targetUserId, cancellationToken);

        var existing = await db.UserRelationships.FirstOrDefaultAsync(
            r => r.Kind == UserRelationshipKind.Follow
                 && r.RequesterUserId == userId
                 && r.AddresseeUserId == targetUserId,
            cancellationToken);
        if (existing is null)
        {
            db.UserRelationships.Add(new UserRelationshipEntity
            {
                RequesterUserId = userId,
                AddresseeUserId = targetUserId,
                Kind = UserRelationshipKind.Follow,
                Status = UserRelationshipStatus.Accepted,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                RespondedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else if (existing.Status != UserRelationshipStatus.Accepted)
        {
            existing.Status = UserRelationshipStatus.Accepted;
            existing.RespondedAtUtc = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await BuildOverviewAsync(userId, cancellationToken);
    }

    public async Task<SocialOverviewDto> UnfollowAsync(
        int userId,
        int targetUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureNotSelf(userId, targetUserId);

        var existing = await db.UserRelationships.FirstOrDefaultAsync(
            r => r.Kind == UserRelationshipKind.Follow
                 && r.RequesterUserId == userId
                 && r.AddresseeUserId == targetUserId,
            cancellationToken);
        if (existing is null)
        {
            return await BuildOverviewAsync(userId, cancellationToken);
        }

        db.UserRelationships.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildOverviewAsync(userId, cancellationToken);
    }

    private async Task<UserRelationshipEntity?> FindFriendAsync(
        int userId,
        int otherUserId,
        CancellationToken cancellationToken) =>
        await db.UserRelationships.FirstOrDefaultAsync(
            r => r.Kind == UserRelationshipKind.Friend
                 && ((r.RequesterUserId == userId && r.AddresseeUserId == otherUserId)
                     || (r.RequesterUserId == otherUserId && r.AddresseeUserId == userId)),
            cancellationToken);

    private async Task EnsureUserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
        {
            throw new ArgumentException("Nie znaleziono użytkownika.");
        }
    }

    private static void EnsureNotSelf(int userId, int otherUserId)
    {
        if (userId == otherUserId)
        {
            throw new ArgumentException("Nie możesz dodać samego siebie.");
        }
    }

    private async Task<SocialOverviewDto> BuildOverviewAsync(int userId, CancellationToken cancellationToken)
    {
        var relations = await LoadRelationsAsync(userId, cancellationToken);
        var otherIds = relations
            .SelectMany(r => new[] { r.RequesterUserId, r.AddresseeUserId })
            .Where(id => id != userId)
            .Distinct()
            .ToList();

        var users = await db.Users.AsNoTracking()
            .Where(u => otherIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        SocialUserDto? MapId(int id) =>
            users.TryGetValue(id, out var user) ? Map(user, userId, relations) : null;

        var friends = relations
            .Where(r => r.Kind == UserRelationshipKind.Friend && r.Status == UserRelationshipStatus.Accepted)
            .Select(r => r.RequesterUserId == userId ? r.AddresseeUserId : r.RequesterUserId)
            .Select(MapId)
            .OfType<SocialUserDto>()
            .OrderBy(u => u.DisplayName)
            .ToList();

        var incoming = relations
            .Where(r => r.Kind == UserRelationshipKind.Friend
                        && r.Status == UserRelationshipStatus.Pending
                        && r.AddresseeUserId == userId)
            .Select(r => MapId(r.RequesterUserId))
            .OfType<SocialUserDto>()
            .OrderBy(u => u.DisplayName)
            .ToList();

        var outgoing = relations
            .Where(r => r.Kind == UserRelationshipKind.Friend
                        && r.Status == UserRelationshipStatus.Pending
                        && r.RequesterUserId == userId)
            .Select(r => MapId(r.AddresseeUserId))
            .OfType<SocialUserDto>()
            .OrderBy(u => u.DisplayName)
            .ToList();

        var following = relations
            .Where(r => r.Kind == UserRelationshipKind.Follow
                        && r.Status == UserRelationshipStatus.Accepted
                        && r.RequesterUserId == userId)
            .Select(r => MapId(r.AddresseeUserId))
            .OfType<SocialUserDto>()
            .OrderBy(u => u.DisplayName)
            .ToList();

        var followers = relations
            .Where(r => r.Kind == UserRelationshipKind.Follow
                        && r.Status == UserRelationshipStatus.Accepted
                        && r.AddresseeUserId == userId)
            .Select(r => MapId(r.RequesterUserId))
            .OfType<SocialUserDto>()
            .OrderBy(u => u.DisplayName)
            .ToList();

        return new SocialOverviewDto
        {
            FriendsCount = friends.Count,
            FollowingCount = following.Count,
            FollowersCount = followers.Count,
            IncomingCount = incoming.Count,
            Friends = friends,
            Incoming = incoming,
            Outgoing = outgoing,
            Following = following,
            Followers = followers
        };
    }

    private async Task<List<UserRelationshipEntity>> LoadRelationsAsync(
        int userId,
        CancellationToken cancellationToken) =>
        await db.UserRelationships.AsNoTracking()
            .Where(r => r.RequesterUserId == userId || r.AddresseeUserId == userId)
            .ToListAsync(cancellationToken);

    private static SocialUserDto Map(
        UserEntity user,
        int viewerId,
        IReadOnlyList<UserRelationshipEntity> relations)
    {
        var friend = relations.FirstOrDefault(r =>
            r.Kind == UserRelationshipKind.Friend
            && ((r.RequesterUserId == viewerId && r.AddresseeUserId == user.Id)
                || (r.RequesterUserId == user.Id && r.AddresseeUserId == viewerId)));

        var relation = FriendRelation.None;
        if (friend is { Status: UserRelationshipStatus.Accepted })
        {
            relation = FriendRelation.Friends;
        }
        else if (friend is { Status: UserRelationshipStatus.Pending } && friend.RequesterUserId == viewerId)
        {
            relation = FriendRelation.OutgoingRequest;
        }
        else if (friend is { Status: UserRelationshipStatus.Pending } && friend.AddresseeUserId == viewerId)
        {
            relation = FriendRelation.IncomingRequest;
        }

        return new SocialUserDto
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
                && DateTimeOffset.UtcNow - seen <= OnlineWindow,
            FriendRelation = relation,
            IsFollowing = relations.Any(r =>
                r.Kind == UserRelationshipKind.Follow
                && r.Status == UserRelationshipStatus.Accepted
                && r.RequesterUserId == viewerId
                && r.AddresseeUserId == user.Id),
            IsFollower = relations.Any(r =>
                r.Kind == UserRelationshipKind.Follow
                && r.Status == UserRelationshipStatus.Accepted
                && r.RequesterUserId == user.Id
                && r.AddresseeUserId == viewerId)
        };
    }
}
