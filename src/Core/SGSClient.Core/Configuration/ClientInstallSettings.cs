namespace SGSClient.Core.Configuration;

public sealed class ClientInstallSettings
{
    public const string SectionName = "Client:Install";

    /// <summary>Root folder for installed game builds. Default: %LocalAppData%/SGSClient/games</summary>
    /// <remarks>Overridden by user-selected library folder in Settings (client-settings.json).</remarks>
    public string? GamesRoot { get; set; }

    public static string GetDefaultGamesRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SGSClient",
            "games");
}
