namespace SGSClient.Infrastructure.Server.Persistence.Entities;

public sealed class UserGameEntitlementEntity
{
    public int UserId { get; set; }
    public int GameId { get; set; }
    public DateTimeOffset GrantedAtUtc { get; set; }
    public string Source { get; set; } = EntitlementSources.FreeClaim;

    public UserEntity? User { get; set; }
    public GameEntity? Game { get; set; }
}

public static class EntitlementSources
{
    public const string FreeClaim = "FreeClaim";
}
