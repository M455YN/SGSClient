namespace SGSClient.Core.Interfaces;

/// <summary>Resolves registered Steam-like library folders.</summary>
public interface ILibraryPathProvider
{
    IReadOnlyList<string> GetLibraryFolders();

    /// <summary>Primary folder used for new installs (first registered folder).</summary>
    string GetPrimaryLibraryFolder();
}
