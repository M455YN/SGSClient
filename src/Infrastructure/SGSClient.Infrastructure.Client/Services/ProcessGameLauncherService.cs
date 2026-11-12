using SGSClient.Core.Localization;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class ProcessGameLauncherService(IServiceScopeFactory scopes) : IGameLauncherService, IDisposable
{
    private readonly ConcurrentDictionary<int, ActiveSession> _byGameId = new();
    private readonly ConcurrentDictionary<string, int> _byExe = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorLoop;

    public bool IsRunning(int gameId) =>
        _byGameId.TryGetValue(gameId, out var session) && !session.Completed;

    public void StartMonitoring()
    {
        if (_monitorLoop is not null) return;
        _monitorCts = new CancellationTokenSource();
        _monitorLoop = MonitorInstalledGamesAsync(_monitorCts.Token);
    }

    public void Dispose()
    {
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
    }

    public async Task<GameLaunchResult> LaunchAsync(
        GameLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExecutablePath);

        var title = string.IsNullOrWhiteSpace(request.Title) ? Loc.T("Gra") : request.Title.Trim();
        string exe;
        try
        {
            exe = Path.GetFullPath(request.ExecutablePath);
        }
        catch
        {
            return GameLaunchResult.NotFound();
        }

        if (!File.Exists(exe))
        {
            return GameLaunchResult.NotFound();
        }

        if (IsRunning(request.GameId) || IsExeTracked(exe) || IsExeRunningOnOs(exe))
        {
            return GameLaunchResult.AlreadyRunning(title);
        }

        var session = new ActiveSession(request.GameId, exe, DateTimeOffset.UtcNow);
        if (!_byGameId.TryAdd(request.GameId, session))
        {
            return GameLaunchResult.AlreadyRunning(title);
        }

        _byExe[Normalize(exe)] = request.GameId;

        var sdkEnvironment = BuildSdkEnvironment(request.GameId, request.LobbyCode);

        Process? process;
        try
        {
            process = StartProcess(exe, sdkEnvironment);
        }
        catch
        {
            RemoveSession(session);
            return GameLaunchResult.Failed();
        }

        if (process is null)
        {
            RemoveSession(session);
            return GameLaunchResult.Failed();
        }

        session.ProcessId = process.Id;
        await TouchLastPlayedAsync(request.GameId, session.StartedAtUtc, cancellationToken);
        _ = TrackUntilExitAsync(session, process);
        return GameLaunchResult.Ok();
    }

    /// <summary>
    /// Gives a starting game the address of the local achievement bridge and a per-launch token.
    /// Nothing here touches the network, so it works offline; with no bridge the game just starts without it.
    /// </summary>
    private IReadOnlyDictionary<string, string>? BuildSdkEnvironment(int gameId, string? lobbyCode)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var environment = scope.ServiceProvider.GetService<AchievementBridge>()?.CreateSession(gameId);
            if (environment is null || string.IsNullOrWhiteSpace(lobbyCode))
            {
                return environment;
            }

            return new Dictionary<string, string>(environment) { ["SGS_LOBBY_CODE"] = lobbyCode.Trim() };
        }
        catch
        {
            return null;
        }
    }

    private static Process? StartWith(ProcessStartInfo info, IReadOnlyDictionary<string, string>? environment)
    {
        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                info.Environment[key] = value;
            }
        }

        return Process.Start(info);
    }

    private static Process? StartProcess(string exe, IReadOnlyDictionary<string, string>? environment)
    {
        var workingDirectory = Path.GetDirectoryName(exe);
        var ext = Path.GetExtension(exe);

        // Seed packages use .cmd / .sh so claim → install → play works without a real PE.
        if (OperatingSystem.IsWindows()
            && (string.Equals(ext, ".cmd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ext, ".bat", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                return StartWith(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{exe}\"",
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                }, environment);
            }
            catch
            {
                // fall through
            }
        }

        if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            && (string.Equals(ext, ".sh", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(ext)))
        {
            try
            {
                return StartWith(new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    ArgumentList = { exe },
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory
                }, environment);
            }
            catch
            {
                // fall through
            }
        }

        try
        {
            return StartWith(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            }, environment);
        }
        catch
        {
            // ShellExecute cannot carry environment variables; this last resort starts without them.
            return Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                WorkingDirectory = workingDirectory
            });
        }
    }

    private async Task TrackUntilExitAsync(ActiveSession session, Process process)
    {
        try
        {
            await WaitUntilGameExitsAsync(session.ExecutablePath, process);
        }
        catch
        {
            // still close the session and record whatever time we have
        }
        finally
        {
            try
            {
                process.Dispose();
            }
            catch
            {
            }

            var ended = DateTimeOffset.UtcNow;
            var duration = ended - session.StartedAtUtc;
            session.Completed = true;
            RemoveSession(session);

            if (duration.TotalSeconds >= 1)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var playtime = scope.ServiceProvider.GetRequiredService<IPlaytimeStore>();
                    await playtime.RecordSessionAsync(session.GameId, duration, ended);
                }
                catch
                {
                }
            }
        }
    }

    private static async Task WaitUntilGameExitsAsync(string exe, Process process)
    {
        await Task.Delay(2000);

        var seenRunning = IsExeRunningOnOs(exe);
        try
        {
            seenRunning = seenRunning || !process.HasExited;
        }
        catch
        {
        }

        if (!seenRunning)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline && !IsExeRunningOnOs(exe))
            {
                await Task.Delay(400);
            }
        }

        while (true)
        {
            var osRunning = IsExeRunningOnOs(exe);
            var processAlive = false;
            try
            {
                processAlive = !process.HasExited;
            }
            catch
            {
            }

            if (!osRunning && !processAlive)
            {
                await Task.Delay(1500);
                if (!IsExeRunningOnOs(exe))
                {
                    return;
                }
            }

            await Task.Delay(1000);
        }
    }

    private async Task TouchLastPlayedAsync(int gameId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var library = scope.ServiceProvider.GetService<ILocalLibraryStore>();
            if (library is null)
            {
                return;
            }

            var item = await library.GetByGameIdAsync(gameId, cancellationToken);
            if (item is null)
            {
                return;
            }

            item.LastPlayedUtc = startedAtUtc;
            await library.UpsertAsync(item, cancellationToken);
        }
        catch
        {
        }
    }

    private bool IsExeTracked(string exe) =>
        _byExe.TryGetValue(Normalize(exe), out var gameId)
        && _byGameId.TryGetValue(gameId, out var session)
        && !session.Completed;

    private static bool IsExeRunningOnOs(string exe)
    {
        var name = Path.GetFileNameWithoutExtension(exe);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(name);
        }
        catch
        {
            return false;
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path)
                        && string.Equals(Normalize(path), Normalize(exe), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Access denied: same process name is enough to refuse a second copy.
                    return true;
                }
            }

            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private void RemoveSession(ActiveSession session)
    {
        _byGameId.TryRemove(session.GameId, out _);
        _byExe.TryRemove(Normalize(session.ExecutablePath), out _);
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private async Task MonitorInstalledGamesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                using var scope = scopes.CreateScope();
                var library = scope.ServiceProvider.GetService<ILocalLibraryStore>();
                if (library is null) continue;

                var installed = await library.GetAllAsync(cancellationToken);
                foreach (var item in installed)
                {
                    if (item.InstallState != Core.Enums.InstallState.Installed) continue;
                    if (string.IsNullOrWhiteSpace(item.ExecutablePath)) continue;
                    if (_byGameId.ContainsKey(item.GameId)) continue;

                    string exe;
                    try { exe = Path.GetFullPath(item.ExecutablePath); }
                    catch { continue; }

                    if (!IsExeRunningOnOs(exe)) continue;

                    var session = new ActiveSession(item.GameId, exe, DateTimeOffset.UtcNow);
                    if (!_byGameId.TryAdd(item.GameId, session)) continue;
                    _byExe[Normalize(exe)] = item.GameId;

                    _ = TrackExternalUntilExitAsync(session);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // best-effort monitoring
            }
        }
    }

    private async Task TrackExternalUntilExitAsync(ActiveSession session)
    {
        try
        {
            while (IsExeRunningOnOs(session.ExecutablePath))
            {
                await Task.Delay(3000);
            }
            await Task.Delay(1500);
            if (IsExeRunningOnOs(session.ExecutablePath))
            {
                await TrackExternalUntilExitAsync(session);
                return;
            }
        }
        catch
        {
        }
        finally
        {
            var ended = DateTimeOffset.UtcNow;
            var duration = ended - session.StartedAtUtc;
            session.Completed = true;
            RemoveSession(session);

            if (duration.TotalSeconds >= 1)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var playtime = scope.ServiceProvider.GetRequiredService<IPlaytimeStore>();
                    await playtime.RecordSessionAsync(session.GameId, duration, ended);
                }
                catch { }
            }
        }
    }

    private sealed class ActiveSession(int gameId, string executablePath, DateTimeOffset startedAtUtc)
    {
        public int GameId { get; } = gameId;
        public string ExecutablePath { get; } = executablePath;
        public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;
        public int ProcessId { get; set; }
        public bool Completed { get; set; }
    }
}
