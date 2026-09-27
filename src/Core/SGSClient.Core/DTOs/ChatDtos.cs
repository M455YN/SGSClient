namespace SGSClient.Core.DTOs;

public sealed class ChatThreadDto
{
    public required SocialUserDto Peer { get; init; }
    public string? LastMessagePreview { get; init; }
    public DateTimeOffset? LastMessageAtUtc { get; init; }
    public int UnreadCount { get; init; }
}

public sealed class ChatMessageDto
{
    public long Id { get; init; }
    public int SenderUserId { get; init; }
    public int RecipientUserId { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool IsMine { get; init; }
    public bool IsRead { get; init; }
}

public sealed class ChatConversationDto
{
    public required SocialUserDto Peer { get; init; }
    public IReadOnlyList<ChatMessageDto> Messages { get; init; } = [];
}

public sealed class SendChatMessageRequest
{
    public string Text { get; init; } = string.Empty;
}
