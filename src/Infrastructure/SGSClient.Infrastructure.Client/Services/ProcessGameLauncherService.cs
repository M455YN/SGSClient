using System.Diagnostics;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class ProcessGameLauncherService : IGameLauncherService
{
    public Task LaunchAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Game executable was not found.", executablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath)
        };

        Process.Start(startInfo);
        return Task.CompletedTask;
    }
}
