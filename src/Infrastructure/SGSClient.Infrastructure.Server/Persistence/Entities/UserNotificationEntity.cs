namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class UserNotificationEntity
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public UserEntity? User { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? DataJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
}
