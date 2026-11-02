namespace SGSClient.Core.Configuration;

public sealed class MaintenanceSettings
{
    public const string SectionName = "Maintenance";

    /// <summary>When true, only game downloads stay available; super admins keep full access.</summary>
    public bool Enabled { get; set; }
}
