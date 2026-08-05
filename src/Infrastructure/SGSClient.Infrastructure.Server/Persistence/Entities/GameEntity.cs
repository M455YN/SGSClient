namespace SGSClient.Infrastructure.Server.Persistence.Entities;

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
    public string? IntroUrl { get; set; }
    public bool DraftP { get; set; }
    public bool FeaturedP { get; set; }

    public GameEngineEntity? Engine { get; set; }
    public GameGenreEntity? Genre { get; set; }
    public ICollection<GameBuildEntity> Builds { get; set; } = new List<GameBuildEntity>();
    public ICollection<GameScreenshotEntity> Screenshots { get; set; } = new List<GameScreenshotEntity>();
}
