namespace SGSClient.Infrastructure.Server.Persistence.Entities;

using SGSClient.Core.Enums;

public sealed class UserEntity
{
    public int Id { get; set; }
    public required string AzureSub { get; set; }
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }
    public string? PlayerTag { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastLoginUtc { get; set; }

    /// <summary>Last presence heartbeat from the client (online indicator).</summary>
    public DateTimeOffset? LastSeenUtc { get; set; }

    public string SettingsJson { get; set; } = "{}";

    /// <summary>When true, other players see no achievements on this user's public profile.</summary>
    public bool HideAchievements { get; set; }
}
