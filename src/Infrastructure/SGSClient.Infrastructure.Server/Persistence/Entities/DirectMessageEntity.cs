namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class DirectMessageEntity
{
    public long Id { get; set; }
    public int SenderUserId { get; set; }
    public UserEntity? Sender { get; set; }
    public int RecipientUserId { get; set; }
    public UserEntity? Recipient { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
}
