using SGSClient.Core.Library;

namespace SGSClient.Core.Interfaces;

public interface ILibraryFoldersStore
{
    SgsLibraryFoldersRegistry Load();
    Task SaveAsync(SgsLibraryFoldersRegistry registry, CancellationToken cancellationToken = default);
}

public interface ILibraryFolderService
{
    IReadOnlyList<SgsLibraryFolderEntry> GetFolders();
    Task<SgsLibraryFolderEntry> AddFolderAsync(string folderPath, string? label = null, CancellationToken cancellationToken = default);
    Task RemoveFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    /// <summary>Registers the folder if needed and makes it the first one, i.e. where new installs go.</summary>
    Task SetPrimaryFolderAsync(string folderPath, CancellationToken cancellationToken = default);
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}
