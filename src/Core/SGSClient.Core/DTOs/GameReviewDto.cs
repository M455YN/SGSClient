namespace SGSClient.Core.DTOs;

public sealed class GameReviewDto
{
    public long Id { get; init; }
    public int GameId { get; init; }
    public int AuthorUserId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public string? AuthorAvatarUrl { get; init; }
    public string? AuthorPlayerTag { get; init; }
    public bool Recommended { get; init; }
    public required string Text { get; init; }
    public long? PlaytimeSecondsAtReview { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public bool IsMine { get; init; }
    public int HelpfulCount { get; init; }
    public bool? MyHelpfulVote { get; init; }
    public int CommentCount { get; init; }
}

public sealed class GameReviewsSummaryDto
{
    public int TotalCount { get; init; }
    public int RecommendedCount { get; init; }
    public int NotRecommendedCount { get; init; }
    public double RecommendedPercent { get; init; }
    public GameReviewDto? MyReview { get; init; }
    public bool CanReview { get; init; }
    public IReadOnlyList<GameReviewDto> Reviews { get; init; } = [];
}

public sealed class UpsertGameReviewRequest
{
    public bool Recommended { get; init; }
    public string Text { get; init; } = string.Empty;
}

public sealed class SetReviewVoteRequest
{
    public bool Helpful { get; init; }
}

public sealed class GameReviewCommentDto
{
    public long Id { get; init; }
    public long ReviewId { get; init; }
    public int AuthorUserId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public string? AuthorPlayerTag { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool IsMine { get; init; }
}

public sealed class CreateGameReviewCommentRequest
{
    public string Text { get; init; } = string.Empty;
}
