namespace SGSClient.Core.DTOs;

public sealed class GameBuildVersionDto
{
    public int Id { get; init; }
    public int GameBuildId { get; init; }
    public required string Version { get; init; }
    public required string ExeName { get; init; }
    public required string ZipLink { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }
    public bool IsCurrent { get; init; }
}
