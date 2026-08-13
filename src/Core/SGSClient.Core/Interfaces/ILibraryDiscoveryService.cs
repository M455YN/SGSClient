namespace SGSClient.Core.Interfaces;

public sealed class LibraryScanResult
{
    public int DiscoveredInstalls { get; init; }
    public int UpdatedEntries { get; init; }
    public int MarkedNotInstalled { get; init; }
}

/// <summary>Scans the library folder on disk and syncs install state to SQLite.</summary>
public interface ILibraryDiscoveryService
{
    Task<LibraryScanResult> ScanAsync(CancellationToken cancellationToken = default);
}
