namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class GameEngineEntity
{
    public int Id { get; set; }
    public required string Symbol { get; set; }
    public required string Name { get; set; }

    public ICollection<GameEntity> Games { get; set; } = new List<GameEntity>();
}
