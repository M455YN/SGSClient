using SGSClient.Core.Enums;

namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class GameReviewEntity
{
    public long Id { get; set; }
    public int GameId { get; set; }
    public GameEntity? Game { get; set; }
    public int AuthorUserId { get; set; }
    public UserEntity? Author { get; set; }

    /// <summary>Steam-like thumbs up / thumbs down.</summary>
    public bool Recommended { get; set; }

    public string Text { get; set; } = string.Empty;
    public long? PlaytimeSecondsAtReview { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
