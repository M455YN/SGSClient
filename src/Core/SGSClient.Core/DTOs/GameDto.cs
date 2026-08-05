namespace SGSClient.Core.DTOs;

/// <summary>
/// Catalog game. Downloadable binaries live in <see cref="Builds"/>.
/// </summary>
public sealed class GameDto
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? Description { get; init; }
    public string? HardwareRequirements { get; init; }
    public string? OtherInformation { get; init; }
    public int? EngineId { get; init; }
    public int? TypeId { get; init; }
    public string? GenreSymbol { get; init; }
    public string? GenreName { get; init; }
    public string? EngineSymbol { get; init; }
    public string? EngineName { get; init; }
    public string? CoverUrl { get; init; }
    public string? IntroUrl { get; init; }
    public IReadOnlyList<string> ScreenshotUrls { get; init; } = [];
    public bool DraftP { get; init; }
    public bool FeaturedP { get; init; }
    public IReadOnlyList<GameBuildDto> Builds { get; init; } = [];
}
