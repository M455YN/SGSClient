using SGSClient.Core.Enums;

namespace SGSClient.Core.DTOs;

public sealed class UserProfileDto
{
    public int Id { get; init; }
    public required string AzureSub { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Bio { get; init; }
    public string? Location { get; init; }
    public string? PlayerTag { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastLoginUtc { get; init; }
    public UserRole Role { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsSuperAdmin => Role >= UserRole.SuperAdmin;
    public bool CanPublish => Role >= UserRole.Creator;
    public bool NeedsPlayerTag => string.IsNullOrWhiteSpace(PlayerTag);
}
