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

    /// <summary>Position on the home page featured rail; negative clears it.</summary>
    public int? FeaturedOrder { get; init; }

    public string? StudioName { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? DiscordUrl { get; init; }
    public string? SupportUrl { get; init; }
    public GameReleaseStatus? ReleaseStatus { get; init; }

    /// <summary>Release day; <see cref="ClearReleaseDate"/> removes it.</summary>
    public DateTimeOffset? ReleaseDateUtc { get; init; }
    public bool ClearReleaseDate { get; init; }
    public GameAgeRating? AgeRating { get; init; }
    public GameFeatures? Features { get; init; }

    /// <summary>Full replacement of the language list; empty list clears it.</summary>
    public IReadOnlyList<string>? Languages { get; init; }

    /// <summary>Full replacement of the tag list; empty list clears it.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Admin-only: transfer catalog ownership to another user.
    /// Ignored for creator (owned) updates.
    /// </summary>
    public int? OwnerUserId { get; init; }
}
