namespace SGSClient.Core.Interfaces;

public interface IGameLauncherService
{
    Task LaunchAsync(string executablePath, CancellationToken cancellationToken = default);
}
