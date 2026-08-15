using SGSClient.Core.Enums;

namespace SGSClient.Core.DTOs;

/// <summary>Lightweight DLC row for properties / store lists.</summary>
public sealed class GameDlcDto
{
    public int Id { get; init; }
    public int ParentGameId { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? CoverUrl { get; init; }
    public long PackageSizeBytes { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public int PreferredBuildId { get; init; }
    public GamePlatform PreferredPlatform { get; init; }
    public string? PreferredArchitecture { get; init; }
    public string? PreferredVersion { get; init; }
}

/// <summary>Owner/admin metadata update (partial — null fields are left unchanged).</summary>
public sealed class UpdateGameRequestDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? HardwareRequirements { get; init; }
    public string? OtherInformation { get; init; }
    /// <summary>Genre slug from taxonomy; empty string clears.</summary>
    public string? Genre { get; init; }
    /// <summary>Engine slug from taxonomy; empty string clears.</summary>
    public string? Engine { get; init; }
    public bool? DraftP { get; init; }
    public bool? FeaturedP { get; init; }
}
