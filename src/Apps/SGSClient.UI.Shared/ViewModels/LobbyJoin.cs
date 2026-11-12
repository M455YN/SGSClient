namespace SGSClient.UI.ViewModels;

/// <summary>
/// One place that knows how to join a friend's lobby (start the game with its code), so an invite
/// notification and the friends list do the same thing. The shell sets <see cref="Handler"/> on start-up.
/// </summary>
public static class LobbyJoin
{
    public static Func<int, string, Task>? Handler { get; set; }

    public static Task RequestAsync(int gameId, string code) => Handler?.Invoke(gameId, code) ?? Task.CompletedTask;
}
