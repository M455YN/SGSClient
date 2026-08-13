using System.Text.Json;

namespace SGSClient.Core.Library;

/// <summary>
/// Marker file at the root of each library folder (Steam <c>libraryfolder.vdf</c> equivalent).
/// </summary>
public sealed class SgsLibraryFolderMarker
{
    public const string FileName = "libraryfolder.json";

    public int Format { get; set; } = SgsLibraryFormats.Current;
    public required string ContentId { get; set; }
    public string? Label { get; set; }
    public string? Launcher { get; set; }

    public static JsonSerializerOptions JsonOptions { get; } = SgsLibraryFormats.JsonOptions;
}
