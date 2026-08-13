using System.Text.Json;

namespace SGSClient.Core.Library;

public static class SgsLibraryFormats
{
    public const int Current = 1;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

public static class SgsLibraryLayout
{
    public const string AppsFolder = "sgsapps";
    public const string CommonFolder = "common";
    public const string ManifestsFolder = "manifests";

    public static string GetAppsRoot(string libraryRoot) =>
        Path.Combine(libraryRoot, AppsFolder);

    public static string GetCommonRoot(string libraryRoot) =>
        Path.Combine(libraryRoot, AppsFolder, CommonFolder);

    public static string GetManifestsRoot(string libraryRoot) =>
        Path.Combine(libraryRoot, AppsFolder, ManifestsFolder);

    public static string GetManifestFileName(int gameId, int buildId) =>
        $"game-{gameId}-build-{buildId}.json";
}
