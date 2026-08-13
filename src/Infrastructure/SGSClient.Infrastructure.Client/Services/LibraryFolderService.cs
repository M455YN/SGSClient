using System.Diagnostics;
using System.Text.Json;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class LibraryFoldersStore : ILibraryFoldersStore
{
    private readonly string _registryPath;

    public LibraryFoldersStore()
    {
        var root = Path.GetDirectoryName(DependencyInjection.GetDefaultLibraryDbPath()) ?? string.Empty;
        _registryPath = Path.Combine(root, SgsLibraryFoldersRegistry.FileName);
    }

    public SgsLibraryFoldersRegistry Load()
    {
        if (!File.Exists(_registryPath))
        {
            return new SgsLibraryFoldersRegistry();
        }

        try
        {
            var json = File.ReadAllText(_registryPath);
            return JsonSerializer.Deserialize<SgsLibraryFoldersRegistry>(json, SgsLibraryFoldersRegistry.JsonOptions)
                ?? new SgsLibraryFoldersRegistry();
        }
        catch
        {
            return new SgsLibraryFoldersRegistry();
        }
    }

    public async Task SaveAsync(SgsLibraryFoldersRegistry registry, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_registryPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        registry.Format = SgsLibraryFormats.Current;
        var json = JsonSerializer.Serialize(registry, SgsLibraryFoldersRegistry.JsonOptions);
        await File.WriteAllTextAsync(_registryPath, json, cancellationToken).ConfigureAwait(false);
    }
}

internal static class SgsLibraryFolderMarkerIO
{
    public static async Task WriteAsync(string libraryRoot, SgsLibraryFolderEntry entry, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(libraryRoot);
        Directory.CreateDirectory(SgsLibraryLayout.GetCommonRoot(libraryRoot));
        Directory.CreateDirectory(SgsLibraryLayout.GetManifestsRoot(libraryRoot));

        var marker = new SgsLibraryFolderMarker
        {
            ContentId = entry.ContentId,
            Label = entry.Label,
            Launcher = GetLauncherPath()
        };

        var path = Path.Combine(libraryRoot, SgsLibraryFolderMarker.FileName);
        var json = JsonSerializer.Serialize(marker, SgsLibraryFolderMarker.JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public static bool TryRead(string libraryRoot, out SgsLibraryFolderMarker? marker)
    {
        marker = null;
        var path = Path.Combine(libraryRoot, SgsLibraryFolderMarker.FileName);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            marker = JsonSerializer.Deserialize<SgsLibraryFolderMarker>(json, SgsLibraryFolderMarker.JsonOptions);
            return marker is not null;
        }
        catch
        {
            return false;
        }
    }

    public static string GenerateContentId() =>
        Random.Shared.NextInt64(1_000_000_000_000_000, long.MaxValue).ToString();

    private static string? GetLauncherPath()
    {
        try
        {
            return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class LibraryFolderService(
    ILibraryFoldersStore registryStore,
    IClientUserSettingsStore userSettings) : ILibraryFolderService
{
    public IReadOnlyList<SgsLibraryFolderEntry> GetFolders() =>
        registryStore.Load().Folders;

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        var registry = registryStore.Load();
        if (registry.Folders.Count == 0)
        {
            var legacy = userSettings.Load().LibraryFolder;
            if (!string.IsNullOrWhiteSpace(legacy))
            {
                await AddFolderAsync(legacy, cancellationToken: cancellationToken).ConfigureAwait(false);
                var settings = userSettings.Load();
                settings.LibraryFolder = null;
                await userSettings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
                return;
            }

            await AddFolderAsync(ClientInstallSettings.GetDefaultGamesRoot(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        foreach (var folder in registry.Folders.ToList())
        {
            if (!Directory.Exists(folder.Path))
            {
                continue;
            }

            if (!SgsLibraryFolderMarkerIO.TryRead(folder.Path, out _))
            {
                await SgsLibraryFolderMarkerIO.WriteAsync(folder.Path, folder, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<SgsLibraryFolderEntry> AddFolderAsync(
        string folderPath,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Path.GetFullPath(folderPath);
        var registry = registryStore.Load();

        var existing = registry.Folders.FirstOrDefault(f =>
            string.Equals(Path.GetFullPath(f.Path), normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var entry = new SgsLibraryFolderEntry
        {
            Path = normalized,
            ContentId = SgsLibraryFolderMarkerIO.GenerateContentId(),
            Label = label
        };

        registry.Folders.Add(entry);
        await registryStore.SaveAsync(registry, cancellationToken).ConfigureAwait(false);
        await SgsLibraryFolderMarkerIO.WriteAsync(normalized, entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    public async Task RemoveFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        var normalized = Path.GetFullPath(folderPath);
        var registry = registryStore.Load();
        registry.Folders.RemoveAll(f =>
            string.Equals(Path.GetFullPath(f.Path), normalized, StringComparison.OrdinalIgnoreCase));
        await registryStore.SaveAsync(registry, cancellationToken).ConfigureAwait(false);
    }
}
