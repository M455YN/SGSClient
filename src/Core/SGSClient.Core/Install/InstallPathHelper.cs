namespace SGSClient.Core.Install;

using SGSClient.Core.Enums;
using SGSClient.Core.Library;

/// <summary>Shared path helpers for client-side game installation.</summary>
public static class InstallPathHelper
{
    /// <summary>New layout: {libraryRoot}/sgsapps/common/{symbol}/{version}/</summary>
    public static string GetInstallDirectory(string libraryRoot, string symbol, string version) =>
        Path.Combine(SgsLibraryLayout.GetCommonRoot(libraryRoot), symbol, version);

    /// <summary>Legacy layout: {libraryRoot}/{symbol}/{version}/</summary>
    public static string GetLegacyInstallDirectory(string libraryRoot, string symbol, string version) =>
        Path.Combine(libraryRoot, symbol, version);

    public static IEnumerable<string> GetInstallDirectoryCandidates(string libraryRoot, string symbol, string version)
    {
        yield return GetInstallDirectory(libraryRoot, symbol, version);
        yield return GetLegacyInstallDirectory(libraryRoot, symbol, version);
    }

    public static string CombineExecutablePath(string installDirectory, string exeRelativePath)
    {
        var normalized = exeRelativePath.Replace('\\', '/').TrimStart('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Path.Combine([installDirectory, .. parts]);
    }

    public static string ResolveDownloadUrl(string apiBaseUrl, string zipLink)
    {
        if (string.IsNullOrWhiteSpace(zipLink))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(zipLink, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        var baseUri = apiBaseUrl.TrimEnd('/') + "/";
        var relative = zipLink.TrimStart('/');
        return new Uri(new Uri(baseUri), relative).ToString();
    }

    public static bool TryParsePlatform(string? value, out GamePlatform platform)
    {
        platform = GamePlatform.Windows;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "windows" or "win" => Assign(GamePlatform.Windows, out platform),
            "linux" => Assign(GamePlatform.Linux, out platform),
            "macos" or "mac" or "osx" => Assign(GamePlatform.MacOS, out platform),
            _ => Enum.TryParse(value, ignoreCase: true, out platform)
        };
    }

    private static bool Assign(GamePlatform value, out GamePlatform platform)
    {
        platform = value;
        return true;
    }
}
