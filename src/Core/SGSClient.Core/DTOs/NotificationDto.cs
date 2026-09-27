namespace SGSClient.Core.DTOs;

public sealed class NotificationDto
{
    public long Id { get; init; }
    public required string Kind { get; init; }
    public required string Title { get; init; }
    public string? Body { get; init; }
    public string? DataJson { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? ReadAtUtc { get; init; }
    public bool IsRead => ReadAtUtc is not null;
}
