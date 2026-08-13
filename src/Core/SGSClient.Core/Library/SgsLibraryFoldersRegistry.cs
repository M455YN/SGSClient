using System.Text.Json;

namespace SGSClient.Core.Library;

/// <summary>
/// Central registry of library folders (Steam <c>libraryfolders.vdf</c> equivalent).
/// Stored in %LocalAppData%/SGSClient/libraryfolders.json.
/// </summary>
public sealed class SgsLibraryFoldersRegistry
{
    public const string FileName = "libraryfolders.json";

    public int Format { get; set; } = SgsLibraryFormats.Current;
    public List<SgsLibraryFolderEntry> Folders { get; set; } = [];

    public static JsonSerializerOptions JsonOptions { get; } = SgsLibraryFormats.JsonOptions;
}
