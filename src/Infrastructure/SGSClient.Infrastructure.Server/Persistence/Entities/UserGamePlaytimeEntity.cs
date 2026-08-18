namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class UserGamePlaytimeEntity
{
    public int UserId { get; set; }
    public int GameId { get; set; }
    public long TotalSeconds { get; set; }
    public DateTimeOffset? LastPlayedUtc { get; set; }
    public int SessionCount { get; set; }

    public UserEntity? User { get; set; }
    public GameEntity? Game { get; set; }
}
