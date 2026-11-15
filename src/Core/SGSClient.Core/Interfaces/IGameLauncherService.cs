using SGSClient.Core.Models;

namespace SGSClient.Core.Interfaces;

public interface IGameLauncherService
{
    Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);

    bool IsRunning(int gameId);

    /// <summary>Raised with the game id once a play session ended and its time was stored.</summary>
    event Action<int>? SessionEnded;
}
