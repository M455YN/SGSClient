namespace SGSClient.Core.Configuration;

public sealed class GameStorageSettings
{
    public const string SectionName = "Games";

    /// <summary>Absolute path inside the container/host where ZIP packages are stored.</summary>
    public string StoragePath { get; set; } = "App_Data/games";

    /// <summary>Public base URL used in ZipLink (e.g. https://api.example/api/games/packages).</summary>
    public string PublicBaseUrl { get; set; } = "/api/games/packages";

    /// <summary>How many ZIP versions to keep per platform/arch build slot (older ones are deleted).</summary>
    public int RetentionCount { get; set; } = 3;

    /// <summary>Public base URL for cover/intro/screenshots (e.g. /api/games/media).</summary>
    public string MediaPublicBaseUrl { get; set; } = "/api/games/media";

    /// <summary>
    /// Path to the pre-built launcher stub exe. When set, the publish pipeline wraps
    /// the game executable so it can only be launched through SGSClient.
    /// </summary>
    public string? LauncherStubPath { get; set; }
}
