namespace SGSClient.Core.Library;

/// <summary>Raised when a creator publishes or edits a game, so cached store lists reload on next visit instead of after a restart.</summary>
public static class StoreCatalogNotifier
{
    public static event Action? Changed;

    public static void Raise() => Changed?.Invoke();
}
