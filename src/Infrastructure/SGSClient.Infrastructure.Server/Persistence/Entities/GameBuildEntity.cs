using SGSClient.Core.Enums;

namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class GameBuildEntity
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public GameEntity? Game { get; set; }

    public GamePlatform Platform { get; set; }
    public string? Architecture { get; set; }
    public required string CurrentVersion { get; set; }
    public required string ExeName { get; set; }
    public required string ZipLink { get; set; }
    public bool IsDefault { get; set; }
}
