using SGSClient.Core.Enums;

namespace SGSClient.Infrastructure.Client.Persistence.Entities;

public sealed class LocalInstallEntity
{
    public int GameId { get; set; }
    public int BuildId { get; set; }
    public required string Symbol { get; set; }
    public required string Title { get; set; }
    public GamePlatform Platform { get; set; }
    public string? Architecture { get; set; }
    public InstallState InstallState { get; set; } = InstallState.NotInstalled;
    public string? InstallPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string? InstalledVersion { get; set; }
    public string? TargetVersion { get; set; }
    public string? CoverUrl { get; set; }
    public string? LogoUrl { get; set; }
    public string? CustomCoverPath { get; set; }
    public DateTimeOffset? LastPlayedUtc { get; set; }
}
