namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class UserGameWishlistEntity
{
    public int UserId { get; set; }
    public int GameId { get; set; }
    public DateTimeOffset AddedAtUtc { get; set; }

    public UserEntity? User { get; set; }
    public GameEntity? Game { get; set; }
}
