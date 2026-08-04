using SGSClient.Core.Enums;

namespace SGSClient.Core.Models;

public sealed class GameLibraryItem
{
    public int GameId { get; init; }
    public int BuildId { get; init; }
    public required string Symbol { get; init; }
    public required string Title { get; set; }
    public GamePlatform Platform { get; set; }
    public string? Architecture { get; set; }
    public InstallState InstallState { get; set; } = InstallState.NotInstalled;
    public string? InstallPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string? InstalledVersion { get; set; }
    public DateTimeOffset? LastPlayedUtc { get; set; }
}
