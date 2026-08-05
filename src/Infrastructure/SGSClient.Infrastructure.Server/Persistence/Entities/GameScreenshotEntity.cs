namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class GameScreenshotEntity
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int SortOrder { get; set; }
    public required string Url { get; set; }
    public string? SourcePath { get; set; }

    public GameEntity? Game { get; set; }
}
