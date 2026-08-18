using SGSClient.Core.Enums;

namespace SGSClient.Core.DTOs;

public sealed class AdminUserDto
{
    public int Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? PlayerTag { get; init; }
    public UserRole Role { get; init; }

    /// <summary>Role comes from configuration (bootstrap owner) and cannot be changed from the panel.</summary>
    public bool IsRoleLocked { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastLoginUtc { get; init; }
}

/// <summary>Admin-only role assignment.</summary>
public sealed class UpdateUserRoleRequestDto
{
    public UserRole Role { get; init; }
}

/// <summary>Outcome of a role change; <see cref="Error"/> carries the server-side reason on refusal.</summary>
public sealed class AdminRoleUpdateResult
{
    public AdminUserDto? User { get; init; }
    public string? Error { get; init; }
    public bool Success => User is not null;
}
