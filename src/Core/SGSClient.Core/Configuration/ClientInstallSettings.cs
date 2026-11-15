namespace SGSClient.Core.Configuration;

public sealed class ClientInstallSettings
{
    public const string SectionName = "Client:Install";

    /// <summary>Root folder for installed game builds. Default: %UserProfile%/SGSClient/Games</summary>
    /// <remarks>Overridden by user-selected library folder in Settings (client-settings.json).</remarks>
    public string? GamesRoot { get; set; }

    /// <summary>When true, library load auto-enqueues updates for installed games with a newer catalog version.</summary>
    public bool AutoUpdateEnabled { get; set; } = true;

    // Not under LocalAppData: a packaged (MSIX) app has that folder redirected into its package, so uninstalling deleted the games.
    public static string GetDefaultGamesRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "SGSClient",
            "Games");
}
