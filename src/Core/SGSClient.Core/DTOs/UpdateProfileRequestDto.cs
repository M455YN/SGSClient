namespace SGSClient.Core.DTOs;

public sealed class UpdateProfileRequestDto
{
    public string? DisplayName { get; init; }
    public string? Bio { get; init; }
    public string? Location { get; init; }
    public string? AvatarUrl { get; init; }
    public string? PlayerTag { get; init; }

    /// <summary>null = leave the privacy setting as it is.</summary>
    public bool? HideAchievements { get; init; }

    /// <summary>null = leave the privacy setting as it is.</summary>
    public bool? HideGameActivity { get; init; }
}
