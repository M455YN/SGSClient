namespace SGSClient.Core.DTOs;

public sealed class PublicUserProfileDto
{
    public required SocialUserDto User { get; init; }
    public bool IsOwnProfile { get; init; }

    /// <summary>The player chose to hide their achievements (the owner still sees them).</summary>
    public bool AchievementsHidden { get; init; }

    /// <summary>Per-game unlock counts; always empty for other players when hidden.</summary>
    public IReadOnlyList<UserAchievementSummaryDto> Achievements { get; init; } = [];

    public DateTimeOffset MemberSinceUtc { get; init; }

    /// <summary>The player chose to hide their games and playtime (the owner still sees them).</summary>
    public bool GameActivityHidden { get; init; }

    /// <summary>Number of played store games; 0 when hidden.</summary>
    public int GamesCount { get; init; }

    public long TotalPlaytimeSeconds { get; init; }

    /// <summary>Most recently played games first; empty when hidden.</summary>
    public IReadOnlyList<PublicGameActivityDto> Games { get; init; } = [];

    public IReadOnlyList<ProfileCommentDto> Comments { get; init; } = [];
}

public sealed class PublicGameActivityDto
{
    public int GameId { get; init; }
    public required string Title { get; init; }
    public string? CoverUrl { get; init; }
    public string? LogoUrl { get; init; }
    public string? HeaderUrl { get; init; }
    public long TotalSeconds { get; init; }
    public DateTimeOffset? LastPlayedUtc { get; init; }
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
