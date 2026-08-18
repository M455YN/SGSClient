namespace SGSClient.Core.DTOs;

public sealed class RecordPlaytimeSessionRequest
{
    public int GameId { get; init; }
    public long Seconds { get; init; }
    public DateTimeOffset EndedAtUtc { get; init; }
}
