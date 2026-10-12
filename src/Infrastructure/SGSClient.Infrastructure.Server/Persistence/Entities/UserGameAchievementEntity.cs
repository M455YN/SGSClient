namespace SGSClient.Infrastructure.Server.Persistence.Entities;

/// <summary>A player's state for one achievement. The row appears on first progress or unlock.</summary>
public sealed class UserGameAchievementEntity
{
    public int UserId { get; set; }
    public int AchievementId { get; set; }
    public int Progress { get; set; }

    /// <summary>null = still locked; set once, on the first unlock.</summary>
    public DateTimeOffset? UnlockedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public UserEntity? User { get; set; }
    public GameAchievementEntity? Achievement { get; set; }
}
