namespace SGSClient.Core.DTOs;

/// <summary>Read-only user row for admin panel.</summary>
public sealed class AdminUserDto
{
    public int Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastLoginUtc { get; init; }
}
