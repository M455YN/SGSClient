namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class GameBuildVersionEntity
{
    public int Id { get; set; }
    public int GameBuildId { get; set; }
    public GameBuildEntity? GameBuild { get; set; }

    public required string Version { get; set; }
    public required string ExeName { get; set; }
    public required string ZipLink { get; set; }
    public required string FileName { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }

    /// <summary>Creator-written changelog shown in the game's updates tab.</summary>
    public string? ReleaseNotes { get; set; }
}
