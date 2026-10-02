namespace SGSClient.Core.Configuration;

/// <summary>Persisted user preferences (separate from appsettings).</summary>
public sealed class ClientUserSettings
{
    /// <summary>
    /// Root folder for the game library (Steam-like). Games install to
    /// <c>{LibraryFolder}/{symbol}/{version}/</c>.
    /// </summary>
    public string? LibraryFolder { get; set; }

    /// <summary>When true, startup library sync enqueues updates for installed games.</summary>
    public bool AutoUpdateEnabled { get; set; } = true;

    /// <summary>Max download speed in KB/s. 0 = unlimited.</summary>
    public int DownloadMaxKbps { get; set; }
}
