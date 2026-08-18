namespace SGSClient.Core.DTOs;

public sealed class PlaytimeDto
{
    public int GameId { get; init; }
    public long TotalSeconds { get; init; }
    public DateTimeOffset? LastPlayedUtc { get; init; }
    public int SessionCount { get; init; }
}
