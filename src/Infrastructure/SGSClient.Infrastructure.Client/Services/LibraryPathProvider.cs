using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class LibraryPathProvider(
    ILibraryFoldersStore registryStore,
    ILibraryFolderService folderService,
    IOptions<ClientInstallSettings> installOptions) : ILibraryPathProvider
{
    public IReadOnlyList<string> GetLibraryFolders()
    {
        folderService.EnsureInitializedAsync().GetAwaiter().GetResult();

        var registry = registryStore.Load();
        if (registry.Folders.Count > 0)
        {
            return registry.Folders
                .Select(f => Path.GetFullPath(f.Path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var configured = installOptions.Value.GamesRoot;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return [Path.GetFullPath(configured)];
        }

        return [ClientInstallSettings.GetDefaultGamesRoot()];
    }

    public string GetPrimaryLibraryFolder() => GetLibraryFolders().First();
}
