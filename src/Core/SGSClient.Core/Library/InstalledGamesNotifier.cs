namespace SGSClient.Core.Library;

/// <summary>Raised when a game's install state changes (install, uninstall, removal), so the shell can refresh its installed list.</summary>
public static class InstalledGamesNotifier
{
    public static event Action? Changed;

    public static void Raise() => Changed?.Invoke();
}
