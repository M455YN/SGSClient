namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class GameReviewCommentEntity
{
    public long Id { get; set; }
    public long ReviewId { get; set; }
    public GameReviewEntity? Review { get; set; }
    public int AuthorUserId { get; set; }
    public UserEntity? Author { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
