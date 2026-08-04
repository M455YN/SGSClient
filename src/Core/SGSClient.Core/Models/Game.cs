namespace SGSClient.Core.Models;

public sealed class Game
{
    public int Id { get; init; }
    public required string Symbol { get; init; }
    public required string Title { get; set; }
    public string? ExecutablePath { get; set; }
}
