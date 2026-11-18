namespace SGSClient.UI.ViewModels;

/// <summary>
/// One place that knows how to join a friend's lobby (start the game with its code), so an invite
/// notification and the friends list do the same thing. The shell sets <see cref="Handler"/> on start-up.
/// </summary>
public static class LobbyJoin
{
    public static Func<int, string, Task>? Handler { get; set; }

    public static Task RequestAsync(int gameId, string code) => Handler?.Invoke(gameId, code) ?? Task.CompletedTask;

    /// <summary>Joins by a code typed in (e.g. from Discord); the shell sets it next to <see cref="Handler"/>.</summary>
    public static Func<string, Task>? CodeHandler { get; set; }

    public static Task RequestByCodeAsync(string code) => CodeHandler?.Invoke(code) ?? Task.CompletedTask;
}
