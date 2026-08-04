namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class UserEntity
{
    public int Id { get; set; }
    public required string AzureSub { get; set; }
    public required string DisplayName { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Location { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastLoginUtc { get; set; }
    public string SettingsJson { get; set; } = "{}";
}
