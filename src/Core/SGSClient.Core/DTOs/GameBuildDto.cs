using SGSClient.Core.Enums;

namespace SGSClient.Core.DTOs;

public sealed class GameBuildDto
{
    public int Id { get; init; }
    public int GameId { get; init; }
    public GamePlatform Platform { get; init; }
    public string? Architecture { get; init; }
    public required string CurrentVersion { get; init; }
    public required string ExeName { get; init; }
    public required string ZipLink { get; init; }
    public bool IsDefault { get; init; }
}
