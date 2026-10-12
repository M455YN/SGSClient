using SGSClient.Core.Localization;

namespace SGSClient.Core.Models;

public enum GameLaunchStatus
{
    Started = 0,
    AlreadyRunning = 1,
    NotFound = 2,
    Failed = 3
}

public sealed class GameLaunchResult
{
    public required GameLaunchStatus Status { get; init; }
    public required string Message { get; init; }

    public bool Started => Status == GameLaunchStatus.Started;

    public static GameLaunchResult Ok() => new()
    {
        Status = GameLaunchStatus.Started,
        Message = "Uruchomiono."
    };

    public static GameLaunchResult AlreadyRunning(string title)
    {
        var name = string.IsNullOrWhiteSpace(title) ? Loc.T("Gra") : title.Trim();
        return new GameLaunchResult
        {
            Status = GameLaunchStatus.AlreadyRunning,
            Message = Loc.F("„{0}” jest już uruchomiona.", name)
        };
    }

    public static GameLaunchResult NotFound() => new()
    {
        Status = GameLaunchStatus.NotFound,
        Message = Loc.T("Nie znaleziono pliku gry.")
    };

    public static GameLaunchResult Failed(string? message = null) => new()
    {
        Status = GameLaunchStatus.Failed,
        Message = string.IsNullOrWhiteSpace(message)
            ? Loc.T("Nie udało się uruchomić gry.")
            : message
    };
}
