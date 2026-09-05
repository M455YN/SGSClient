namespace SGSClient.Infrastructure.Server.Persistence.Entities;

using SGSClient.Core.Enums;

public sealed class GameEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Title { get; set; }
    public required string Symbol { get; set; }
    public string? Description { get; set; }
    public string? HardwareRequirements { get; set; }
    public string? OtherInformation { get; set; }
    public int? EngineId { get; set; }
    public int? TypeId { get; set; }
    public string? CoverUrl { get; set; }

    /// <summary>Landscape Steam-style store header. Distinct from <see cref="CoverUrl"/> (list thumbnail).</summary>
    public string? HeaderUrl { get; set; }

    /// <summary>Square game logo (e.g. 512×512). Distinct from portrait capsule and landscape header.</summary>
    public string? LogoUrl { get; set; }

    public string? IntroUrl { get; set; }
    public bool DraftP { get; set; }
    public bool FeaturedP { get; set; }

    /// <summary>Position on the home page featured rail. Lower comes first; null sorts last.</summary>
    public int? FeaturedOrder { get; set; }
    public GameContentKind Kind { get; set; } = GameContentKind.Game;
    public int? ParentGameId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? StudioName { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? DiscordUrl { get; set; }
    public string? SupportUrl { get; set; }
    public GameReleaseStatus ReleaseStatus { get; set; } = GameReleaseStatus.Released;
    public DateTimeOffset? ReleaseDateUtc { get; set; }
    public GameAgeRating AgeRating { get; set; } = GameAgeRating.Unrated;
    public GameFeatures Features { get; set; } = GameFeatures.None;

    /// <summary>Comma separated language names, e.g. "Polski,English".</summary>
    public string? LanguagesCsv { get; set; }

    /// <summary>Comma separated lowercase store tags, e.g. "roguelike,pixel art".</summary>
    public string? TagsCsv { get; set; }

    public GameEntity? ParentGame { get; set; }
    public ICollection<GameEntity> Dlcs { get; set; } = new List<GameEntity>();
    public GameEngineEntity? Engine { get; set; }
    public GameGenreEntity? Genre { get; set; }
    public ICollection<GameBuildEntity> Builds { get; set; } = new List<GameBuildEntity>();
    public ICollection<GameScreenshotEntity> Screenshots { get; set; } = new List<GameScreenshotEntity>();
}
