namespace SGSClient.Infrastructure.Client.Persistence.Entities;

public sealed class LocalInstallQueueEntity
{
    public int GameId { get; set; }
    public string? Version { get; set; }
    public bool IsUpdate { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset EnqueuedAtUtc { get; set; }
    public int SortOrder { get; set; }
    public bool IsPaused { get; set; }
}
