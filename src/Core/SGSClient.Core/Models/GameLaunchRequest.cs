namespace SGSClient.Core.Models;

public sealed class GameLaunchRequest
{
    public required int GameId { get; init; }
    public required string Title { get; init; }
    public required string ExecutablePath { get; init; }
}
