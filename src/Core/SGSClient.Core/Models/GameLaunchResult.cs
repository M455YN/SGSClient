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
        var name = string.IsNullOrWhiteSpace(title) ? "Gra" : title.Trim();
        return new GameLaunchResult
        {
            Status = GameLaunchStatus.AlreadyRunning,
            Message = $"„{name}” jest już uruchomiona."
        };
    }

    public static GameLaunchResult NotFound() => new()
    {
        Status = GameLaunchStatus.NotFound,
        Message = "Nie znaleziono pliku gry."
    };

    public static GameLaunchResult Failed(string? message = null) => new()
    {
        Status = GameLaunchStatus.Failed,
        Message = string.IsNullOrWhiteSpace(message)
            ? "Nie udało się uruchomić gry."
            : message
    };
}
