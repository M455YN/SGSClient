namespace SGSClient.Infrastructure.Server.Persistence.Entities;

/// <summary>One package download, used for the creator statistics dashboard.</summary>
public sealed class GameDownloadEntity
{
    public long Id { get; set; }
    public int GameId { get; set; }
    public GameEntity? Game { get; set; }

    public required string Version { get; set; }
    public required string PlatformKey { get; set; }
    public DateTimeOffset DownloadedAtUtc { get; set; }
}
