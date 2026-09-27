namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class UserProfileCommentEntity
{
    public long Id { get; set; }
    public int TargetUserId { get; set; }
    public UserEntity? TargetUser { get; set; }
    public int AuthorUserId { get; set; }
    public UserEntity? Author { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
