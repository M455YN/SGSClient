using System.Text.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;
using SGSClient.Core.Models;
using SGSClient.Core.Packaging;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class LibraryDiscoveryService(
    ILibraryPathProvider paths,
    ILocalLibraryStore library,
    ISgsApiService? api = null) : ILibraryDiscoveryService
{
    public async Task<LibraryScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        var counters = new ScanCounters();
        var seenBuildIds = new HashSet<int>();
        var markedNotInstalled = 0;

        foreach (var libraryRoot in paths.GetLibraryFolders())
        {
            if (!Directory.Exists(libraryRoot))
            {
                continue;
            }

            if (!SgsLibraryFolderMarkerIO.TryRead(libraryRoot, out _))
            {
                continue;
            }

            await ScanCentralManifestsAsync(libraryRoot, seenBuildIds, counters, cancellationToken)
                .ConfigureAwait(false);
            await ScanInstallTreeAsync(libraryRoot, SgsLibraryLayout.GetCommonRoot(libraryRoot), seenBuildIds, counters, cancellationToken)
                .ConfigureAwait(false);
            await ScanInstallTreeAsync(libraryRoot, libraryRoot, seenBuildIds, counters, cancellationToken, legacyRoot: true)
                .ConfigureAwait(false);
        }

        var all = await library.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (var entry in all.Where(x => x.InstallState == InstallState.Installed))
        {
            if (seenBuildIds.Contains(entry.BuildId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.InstallPath) && Directory.Exists(entry.InstallPath))
            {
                continue;
            }

            entry.InstallState = InstallState.NotInstalled;
            entry.InstallPath = null;
            entry.ExecutablePath = null;
            entry.InstalledVersion = null;
            await library.UpsertAsync(entry, cancellationToken).ConfigureAwait(false);
            markedNotInstalled++;
        }

        return new LibraryScanResult
        {
            DiscoveredInstalls = counters.Discovered,
            UpdatedEntries = counters.Updated,
            MarkedNotInstalled = markedNotInstalled
        };
    }

    private sealed class ScanCounters
    {
        public int Discovered;
        public int Updated;
    }

    private async Task ScanCentralManifestsAsync(
        string libraryRoot,
        HashSet<int> seenBuildIds,
        ScanCounters counters,
        CancellationToken cancellationToken)
    {
        var manifestsRoot = SgsLibraryLayout.GetManifestsRoot(libraryRoot);
        if (!Directory.Exists(manifestsRoot))
        {
            return;
        }

        foreach (var manifestFile in Directory.EnumerateFiles(manifestsRoot, "game-*-build-*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = await FromInstallRecordFileAsync(manifestFile, cancellationToken).ConfigureAwait(false);
            if (item is null || item.BuildId <= 0)
            {
                continue;
            }

            await UpsertDiscoveredAsync(item, seenBuildIds, counters, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task ScanInstallTreeAsync(
        string libraryRoot,
        string scanRoot,
        HashSet<int> seenBuildIds,
        ScanCounters counters,
        CancellationToken cancellationToken,
        bool legacyRoot = false)
    {
        if (!Directory.Exists(scanRoot))
        {
            return;
        }

        foreach (var symbolDir in Directory.EnumerateDirectories(scanRoot))
        {
            var symbol = Path.GetFileName(symbolDir);
            if (string.IsNullOrWhiteSpace(symbol)
                || string.Equals(symbol, SgsLibraryLayout.AppsFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var versionDir in Directory.EnumerateDirectories(symbolDir))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var version = Path.GetFileName(versionDir);
                if (string.IsNullOrWhiteSpace(version))
                {
                    continue;
                }

                if (legacyRoot && !IsLegacyInstallDirectory(libraryRoot, symbolDir, versionDir))
                {
                    continue;
                }

                var item = await TryDiscoverInstallAsync(versionDir, symbol, version, libraryRoot, cancellationToken)
                    .ConfigureAwait(false);
                if (item is null || item.BuildId <= 0)
                {
                    continue;
                }

                await UpsertDiscoveredAsync(item, seenBuildIds, counters, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool IsLegacyInstallDirectory(string libraryRoot, string symbolDir, string versionDir)
    {
        var parent = Path.GetDirectoryName(versionDir);
        var grandParent = parent is null ? null : Path.GetDirectoryName(parent);
        if (grandParent is null)
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(grandParent), Path.GetFullPath(libraryRoot), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFileName(parent), SgsLibraryLayout.CommonFolder, StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpsertDiscoveredAsync(
        GameLibraryItem item,
        HashSet<int> seenBuildIds,
        ScanCounters counters,
        CancellationToken cancellationToken)
    {
        if (!seenBuildIds.Add(item.BuildId))
        {
            return;
        }

        var existing = await library.GetByGameIdAsync(item.GameId, cancellationToken).ConfigureAwait(false);
        await library.UpsertAsync(item, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            counters.Discovered++;
        }
        else
        {
            counters.Updated++;
        }
    }

    private async Task<GameLibraryItem?> TryDiscoverInstallAsync(
        string versionDir,
        string symbol,
        string version,
        string libraryRoot,
        CancellationToken cancellationToken)
    {
        var installRecordPath = Path.Combine(versionDir, SgsInstallRecord.FileName);
        if (File.Exists(installRecordPath))
        {
            return await FromInstallRecordAsync(installRecordPath, versionDir, libraryRoot, cancellationToken)
                .ConfigureAwait(false);
        }

        var manifestPath = Path.Combine(versionDir, SgsPackageManifest.FileName);
        if (File.Exists(manifestPath))
        {
            return await FromManifestAsync(manifestPath, versionDir, symbol, version, libraryRoot, cancellationToken)
                .ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<GameLibraryItem?> FromInstallRecordFileAsync(
        string recordPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(recordPath);
        var record = await JsonSerializer.DeserializeAsync<SgsInstallRecord>(
            stream,
            SgsInstallRecord.JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (record is null || string.IsNullOrWhiteSpace(record.InstallPath))
        {
            return null;
        }

        var executablePath = InstallPathHelper.CombineExecutablePath(record.InstallPath, record.Executable);
        if (!File.Exists(executablePath))
        {
            return null;
        }

        return ToLibraryItem(record, record.InstallPath, executablePath);
    }

    private static async Task<GameLibraryItem?> FromInstallRecordAsync(
        string recordPath,
        string installDir,
        string libraryRoot,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(recordPath);
        var record = await JsonSerializer.DeserializeAsync<SgsInstallRecord>(
            stream,
            SgsInstallRecord.JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return null;
        }

        record.LibraryRoot ??= libraryRoot;
        record.InstallPath ??= installDir;

        var executablePath = InstallPathHelper.CombineExecutablePath(installDir, record.Executable);
        if (!File.Exists(executablePath))
        {
            return null;
        }

        return ToLibraryItem(record, installDir, executablePath);
    }

    private async Task<GameLibraryItem?> FromManifestAsync(
        string manifestPath,
        string installDir,
        string symbol,
        string version,
        string libraryRoot,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(manifestPath);
        var (manifest, _) = await SgsPackageManifestSerializer.TryDeserializeAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        if (manifest is null)
        {
            return null;
        }

        if (!InstallPathHelper.TryParsePlatform(manifest.Build.Platform, out var platform))
        {
            platform = GamePlatform.Windows;
        }

        var architecture = string.IsNullOrWhiteSpace(manifest.Build.Architecture) ? "x64" : manifest.Build.Architecture;
        var executablePath = InstallPathHelper.CombineExecutablePath(installDir, manifest.Build.Executable);
        if (!File.Exists(executablePath))
        {
            return null;
        }

        var resolved = await TryResolveFromApiAsync(symbol, version, platform, architecture, cancellationToken)
            .ConfigureAwait(false);

        if (resolved is null || resolved.Value.GameId <= 0 || resolved.Value.BuildId <= 0)
        {
            return null;
        }

        return new GameLibraryItem
        {
            GameId = resolved.Value.GameId,
            BuildId = resolved.Value.BuildId,
            Symbol = symbol,
            Title = manifest.Game.Title,
            Platform = platform,
            Architecture = architecture,
            InstallState = InstallState.Installed,
            InstallPath = installDir,
            ExecutablePath = executablePath,
            InstalledVersion = version
        };
    }

    private static GameLibraryItem ToLibraryItem(SgsInstallRecord record, string installDir, string executablePath) =>
        new()
        {
            GameId = record.GameId,
            BuildId = record.BuildId,
            Symbol = record.Symbol,
            Title = record.Title,
            Platform = record.Platform,
            Architecture = record.Architecture,
            InstallState = InstallState.Installed,
            InstallPath = installDir,
            ExecutablePath = executablePath,
            InstalledVersion = record.Version
        };

    private async Task<(int GameId, int BuildId)?> TryResolveFromApiAsync(
        string symbol,
        string version,
        GamePlatform platform,
        string architecture,
        CancellationToken cancellationToken)
    {
        if (api is null)
        {
            return null;
        }

        try
        {
            var games = await api.GetGamesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var game = games.FirstOrDefault(g => string.Equals(g.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            if (game is null)
            {
                return null;
            }

            var details = await api.GetGameAsync(game.Id, cancellationToken).ConfigureAwait(false);
            if (details is null)
            {
                return null;
            }

            var build = details.Builds.FirstOrDefault(b =>
                string.Equals(b.CurrentVersion, version, StringComparison.OrdinalIgnoreCase)
                && b.Platform == platform
                && string.Equals(b.Architecture ?? "x64", architecture, StringComparison.OrdinalIgnoreCase))
                ?? details.Builds.FirstOrDefault(b => string.Equals(b.CurrentVersion, version, StringComparison.OrdinalIgnoreCase))
                ?? details.Builds.FirstOrDefault(b => b.IsDefault)
                ?? details.Builds.FirstOrDefault();

            if (build is null)
            {
                return null;
            }

            return (game.Id, build.Id);
        }
        catch
        {
            return null;
        }
    }
}
