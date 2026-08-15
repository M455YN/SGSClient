namespace SGSClient.Core.DTOs;

public sealed class UpdateProfileRequestDto
{
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Bio { get; init; }
    public string? Location { get; init; }
    public string? AvatarUrl { get; init; }
}
