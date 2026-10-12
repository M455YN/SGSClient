using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfProfileCommentService(SgsServerDbContext db, IAchievementService achievements)
    : IProfileCommentService
{
    private const int MaxCommentLength = 1000;

    public async Task<PublicUserProfileDto> GetPublicProfileAsync(
        int targetUserId,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono użytkownika.");

        return await BuildProfileAsync(user, viewerUserId, cancellationToken);
    }

    public async Task<PublicUserProfileDto?> GetPublicProfileByTagAsync(
        string playerTag,
        int? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var tag = (playerTag ?? string.Empty).Trim().TrimStart('@').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PlayerTag == tag, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return await BuildProfileAsync(user, viewerUserId, cancellationToken);
    }

    public async Task<ProfileCommentDto> AddCommentAsync(
        int targetUserId,
        int authorUserId,
        string text,
        CancellationToken cancellationToken = default)
    {
        var body = (text ?? string.Empty).Trim();
        if (body.Length is < 1 or > MaxCommentLength)
        {
            throw new ArgumentException($"Komentarz musi mieć od 1 do {MaxCommentLength} znaków.");
        }

        if (!await db.Users.AnyAsync(u => u.Id == targetUserId, cancellationToken))
        {
            throw new KeyNotFoundException("Nie znaleziono użytkownika.");
        }

        var entity = new UserProfileCommentEntity
        {
            TargetUserId = targetUserId,
            AuthorUserId = authorUserId,
            Text = body,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.UserProfileComments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var author = await db.Users.AsNoTracking().FirstAsync(u => u.Id == authorUserId, cancellationToken);
        return MapComment(entity, author, authorUserId, targetUserId);
    }

    public async Task DeleteCommentAsync(
        long commentId,
        int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var comment = await db.UserProfileComments
            .FirstOrDefaultAsync(c => c.Id == commentId, cancellationToken)
            ?? throw new KeyNotFoundException("Nie znaleziono komentarza.");

        if (comment.AuthorUserId != actorUserId && comment.TargetUserId != actorUserId)
        {
            throw new UnauthorizedAccessException("Nie możesz usunąć tego komentarza.");
        }

        db.UserProfileComments.Remove(comment);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<PublicUserProfileDto> BuildProfileAsync(
        UserEntity user,
        int? viewerUserId,
        CancellationToken cancellationToken)
    {
        var relations = viewerUserId is int vid
            ? await db.UserRelationships.AsNoTracking()
                .Where(r => r.RequesterUserId == vid || r.AddresseeUserId == vid)
                .ToListAsync(cancellationToken)
            : [];

        var social = MapSocial(user, viewerUserId ?? 0, relations);
        // Privacy: the owner always sees their own list, everyone else only when it is not hidden.
        var showAchievements = !user.HideAchievements || viewerUserId == user.Id;

        var comments = await db.UserProfileComments.AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.TargetUserId == user.Id)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return new PublicUserProfileDto
        {
            User = social,
            IsOwnProfile = viewerUserId == user.Id,
            AchievementsHidden = user.HideAchievements,
            Achievements = showAchievements
                ? await achievements.GetSummariesAsync(
                    user.Id,
                    includeLocked: viewerUserId == user.Id,
                    cancellationToken: cancellationToken)
                : [],
            Comments = comments
                .Select(c => MapComment(c, c.Author, viewerUserId, user.Id))
                .ToList()
        };
    }

    private static ProfileCommentDto MapComment(
        UserProfileCommentEntity c,
        UserEntity? author,
        int? viewerUserId,
        int targetUserId) => new()
    {
        Id = c.Id,
        TargetUserId = c.TargetUserId,
        AuthorUserId = c.AuthorUserId,
        AuthorDisplayName = author?.DisplayName ?? "Gracz",
        AuthorAvatarUrl = author?.AvatarUrl,
        AuthorPlayerTag = author?.PlayerTag,
        Text = c.Text,
        CreatedAtUtc = c.CreatedAtUtc,
        CanDelete = viewerUserId == c.AuthorUserId || viewerUserId == targetUserId
    };

    private static SocialUserDto MapSocial(
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
                && DateTimeOffset.UtcNow - seen <= FriendsService.OnlineWindow,
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
