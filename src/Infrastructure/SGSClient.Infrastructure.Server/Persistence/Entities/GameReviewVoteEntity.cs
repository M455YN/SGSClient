namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class GameReviewVoteEntity
{
    public long ReviewId { get; set; }
    public GameReviewEntity? Review { get; set; }
    public int UserId { get; set; }
    public UserEntity? User { get; set; }
    public bool IsHelpful { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
