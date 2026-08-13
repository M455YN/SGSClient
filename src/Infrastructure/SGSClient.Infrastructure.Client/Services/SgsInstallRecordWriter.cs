using System.Text.Json;
using SGSClient.Core.Install;
using SGSClient.Core.Library;
using SGSClient.Core.Models;

namespace SGSClient.Infrastructure.Client.Services;

internal static class SgsInstallRecordWriter
{
    public static async Task WriteAsync(
        GameLibraryItem item,
        string libraryRoot,
        string exeRelativePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.InstallPath))
        {
            return;
        }

        var record = new SgsInstallRecord
        {
            GameId = item.GameId,
            BuildId = item.BuildId,
            Symbol = item.Symbol,
            Title = item.Title,
            Platform = item.Platform,
            Architecture = item.Architecture,
            Version = item.InstalledVersion ?? string.Empty,
            Executable = exeRelativePath.Replace('\\', '/'),
            InstalledUtc = DateTimeOffset.UtcNow,
            LibraryRoot = libraryRoot,
            InstallPath = item.InstallPath
        };

        var localPath = Path.Combine(item.InstallPath, SgsInstallRecord.FileName);
        var json = JsonSerializer.Serialize(record, SgsInstallRecord.JsonOptions);
        await File.WriteAllTextAsync(localPath, json, cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(SgsLibraryLayout.GetManifestsRoot(libraryRoot));
        var centralPath = Path.Combine(
            SgsLibraryLayout.GetManifestsRoot(libraryRoot),
            SgsLibraryLayout.GetManifestFileName(item.GameId, item.BuildId));
        await File.WriteAllTextAsync(centralPath, json, cancellationToken).ConfigureAwait(false);
    }

    public static void TryDeleteCentralManifest(string libraryRoot, int gameId, int buildId)
    {
        try
        {
            var path = Path.Combine(
                SgsLibraryLayout.GetManifestsRoot(libraryRoot),
                SgsLibraryLayout.GetManifestFileName(gameId, buildId));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore cleanup errors
        }
    }
}
