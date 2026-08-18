using SGSClient.Core.Models;

namespace SGSClient.Core.Interfaces;

public interface IGameLauncherService
{
    Task<GameLaunchResult> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);

    bool IsRunning(int gameId);
}
