namespace SGSClient.Infrastructure.Client.Persistence.Entities;

public sealed class LocalPlaytimeEntity
{
    public int UserId { get; set; }
    public int GameId { get; set; }
    public long TotalSeconds { get; set; }
    public long UnsyncedSeconds { get; set; }
    public DateTimeOffset? LastPlayedUtc { get; set; }
    public int SessionCount { get; set; }
}
