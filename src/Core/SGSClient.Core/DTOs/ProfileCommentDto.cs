namespace SGSClient.Core.DTOs;

public sealed class PublicUserProfileDto
{
    public required SocialUserDto User { get; init; }
    public bool IsOwnProfile { get; init; }

    /// <summary>The player chose to hide their achievements (the owner still sees them).</summary>
    public bool AchievementsHidden { get; init; }

    /// <summary>Per-game unlock counts; always empty for other players when hidden.</summary>
    public IReadOnlyList<UserAchievementSummaryDto> Achievements { get; init; } = [];

    public IReadOnlyList<ProfileCommentDto> Comments { get; init; } = [];
}

public sealed class ProfileCommentDto
{
    public long Id { get; init; }
    public int TargetUserId { get; init; }
    public int AuthorUserId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public string? AuthorAvatarUrl { get; init; }
    public string? AuthorPlayerTag { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool CanDelete { get; init; }
}

public sealed class CreateProfileCommentRequest
{
    public string Text { get; init; } = string.Empty;
}
