namespace SGSClient.Infrastructure.Server.Persistence.Entities;

/// <summary>A player's value of one stat. The row appears on the first write; until then the default applies.</summary>
public sealed class UserGameStatEntity
{
    public int UserId { get; set; }
    public int StatId { get; set; }
    public double Value { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public UserEntity? User { get; set; }
    public GameStatEntity? Stat { get; set; }
}
