namespace SGSClient.Core.Configuration;

/// <summary>Persisted user preferences (separate from appsettings).</summary>
public sealed class ClientUserSettings
{
    /// <summary>
    /// Root folder for the game library (Steam-like). Games install to
    /// <c>{LibraryFolder}/{symbol}/{version}/</c>.
    /// </summary>
    public string? LibraryFolder { get; set; }
}
