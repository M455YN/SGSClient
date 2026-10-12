namespace SGSClient.Core.Configuration;

public sealed class AchievementSettings
{
    public const string SectionName = "Achievements";

    /// <summary>
    /// HMAC key for game tokens (32+ chars). When empty a random key is generated at startup,
    /// so running games lose their token whenever the API restarts or runs on several instances.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public int TokenLifetimeHours { get; set; } = 12;
}
