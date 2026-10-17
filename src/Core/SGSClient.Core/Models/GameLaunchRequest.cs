namespace SGSClient.Core.Models;

public sealed class GameLaunchRequest
{
    public required int GameId { get; init; }
    public required string Title { get; init; }
    public required string ExecutablePath { get; init; }

    /// <summary>Open the file with the OS default app instead of running it; no session or playtime tracking.</summary>
    public bool OpenAsDocument { get; init; }
}
