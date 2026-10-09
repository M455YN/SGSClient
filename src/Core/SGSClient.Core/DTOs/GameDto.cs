using SGSClient.Core.Enums;
using SGSClient.Core.Packaging;

namespace SGSClient.Core.DTOs;

/// <summary>
/// Catalog game. Downloadable binaries live in <see cref="Builds"/>.
/// </summary>
public sealed class GameDto
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public string? OwnerDisplayName { get; set; }
    public string? OwnerEmail { get; set; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? Description { get; init; }
    public string? HardwareRequirements { get; init; }
    public string? OtherInformation { get; init; }
    public IReadOnlyList<SgsGameTaxonomy.TaxonomyItem> Genres { get; init; } = [];
    public IReadOnlyList<SgsGameTaxonomy.TaxonomyItem> Engines { get; init; } = [];

    /// <summary>First (primary) genre; null when none.</summary>
    public string? GenreSymbol => Genres.Count > 0 ? Genres[0].Symbol : null;
    public string? GenreName => Genres.Count > 0 ? Genres[0].Name : null;
    public string? EngineSymbol => Engines.Count > 0 ? Engines[0].Symbol : null;
    public string? EngineName => Engines.Count > 0 ? Engines[0].Name : null;

    public string GenreNamesText => string.Join(", ", Genres.Select(g => g.Name));
    public string EngineNamesText => string.Join(", ", Engines.Select(e => e.Name));
    public string? CoverUrl { get; init; }

    /// <summary>Landscape store header (Steam-style). Lists use <see cref="CoverUrl"/>.</summary>
    public string? HeaderUrl { get; init; }

    /// <summary>Square logo (e.g. 512×512). Used on small square tiles.</summary>
    public string? LogoUrl { get; init; }

    public string? IntroUrl { get; init; }

    /// <summary>Portrait capsule for lists; falls back to the landscape header.</summary>
    public string? ThumbnailUrl => string.IsNullOrWhiteSpace(CoverUrl) ? HeaderUrl : CoverUrl;

    /// <summary>Wide store art; falls back to the thumbnail.</summary>
    public string? LandscapeUrl => string.IsNullOrWhiteSpace(HeaderUrl) ? CoverUrl : HeaderUrl;

    /// <summary>Square logo; falls back to the portrait capsule.</summary>
    public string? SquareLogoUrl => string.IsNullOrWhiteSpace(LogoUrl) ? ThumbnailUrl : LogoUrl;
    public IReadOnlyList<string> ScreenshotUrls { get; init; } = [];
    public IReadOnlyList<GameScreenshotDto> Screenshots { get; init; } = [];
    public bool DraftP { get; init; }
    public bool FeaturedP { get; init; }
    public int? FeaturedOrder { get; init; }
    public GameContentKind Kind { get; init; } = GameContentKind.Game;
    public int? ParentGameId { get; init; }
    public string? ParentSymbol { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public long PackageSizeBytes { get; init; }

    public string? StudioName { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? DiscordUrl { get; init; }
    public string? SupportUrl { get; init; }
    public GameReleaseStatus ReleaseStatus { get; init; } = GameReleaseStatus.Released;
    public DateTimeOffset? ReleaseDateUtc { get; init; }
    public GameAgeRating AgeRating { get; init; } = GameAgeRating.Unrated;
    public GameFeatures Features { get; init; } = GameFeatures.None;
    public string? LanguagesCsv { get; init; }
    public string? TagsCsv { get; init; }

    public IReadOnlyList<string> Languages => CsvList.Split(LanguagesCsv);
    public IReadOnlyList<string> Tags => CsvList.Split(TagsCsv);

    public IReadOnlyList<GameBuildDto> Builds { get; init; } = [];
    public IReadOnlyList<GameDlcDto> Dlcs { get; init; } = [];
}
