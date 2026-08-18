namespace SGSClient.Core.Models;

public sealed class PlaytimeRecord
{
    public required int GameId { get; init; }
    public long TotalSeconds { get; init; }
    public DateTimeOffset? LastPlayedUtc { get; init; }
    public int SessionCount { get; init; }
}
