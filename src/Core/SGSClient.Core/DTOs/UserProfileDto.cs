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
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? LastLoginUtc { get; init; }
    public bool IsAdmin { get; init; }
}
