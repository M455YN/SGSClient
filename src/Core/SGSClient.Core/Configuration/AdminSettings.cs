namespace SGSClient.Core.Configuration;

public sealed class AdminSettings
{
    public const string SectionName = "Admin";

    /// <summary>Emails allowed to use admin endpoints (case-insensitive).</summary>
    public string[] Emails { get; set; } = ["m455yn@outlook.com"];
}
