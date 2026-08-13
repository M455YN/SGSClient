namespace SGSClient.Core.Library;

public sealed class SgsLibraryFolderEntry
{
    public required string Path { get; set; }
    public required string ContentId { get; set; }
    public string? Label { get; set; }
}
