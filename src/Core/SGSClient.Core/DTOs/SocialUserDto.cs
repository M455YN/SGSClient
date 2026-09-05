using SGSClient.Core.Enums;

namespace SGSClient.Core.DTOs;

public sealed class SocialUserDto
{
    public int Id { get; init; }
    public required string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Location { get; init; }
    public string? Bio { get; init; }
    public string? PlayerTag { get; init; }
    public DateTimeOffset? LastLoginUtc { get; init; }
    public DateTimeOffset? LastSeenUtc { get; init; }

    /// <summary>True when <see cref="LastSeenUtc"/> is within the last 5 minutes.</summary>
    public bool IsOnline { get; init; }

    public FriendRelation FriendRelation { get; init; }
    public bool IsFollowing { get; init; }
    public bool IsFollower { get; init; }
}
