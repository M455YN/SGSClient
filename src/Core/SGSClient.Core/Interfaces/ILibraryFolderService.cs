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
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
}
