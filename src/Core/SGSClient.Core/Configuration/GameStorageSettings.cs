namespace SGSClient.Core.Configuration;

public sealed class GameStorageSettings
{
    public const string SectionName = "Games";

    /// <summary>Absolute path inside the container/host where ZIP packages are stored.</summary>
    public string StoragePath { get; set; } = "App_Data/games";

    /// <summary>Public base URL used in ZipLink (e.g. https://api.example/api/games/packages).</summary>
    public string PublicBaseUrl { get; set; } = "/api/games/packages";

    /// <summary>Public base URL for cover/intro/screenshots (e.g. /api/games/media).</summary>
    public string MediaPublicBaseUrl { get; set; } = "/api/games/media";
}
