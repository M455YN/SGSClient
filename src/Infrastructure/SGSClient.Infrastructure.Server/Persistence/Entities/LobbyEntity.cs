namespace SGSClient.Infrastructure.Server.Persistence.Entities;

/// <summary>A short-lived game room; rows past <c>ILobbyService.Lifetime</c> are deleted on the next lobby call.</summary>
public sealed class LobbyEntity
{
    public string Code { get; set; } = string.Empty;
    public int GameId { get; set; }
    public int HostUserId { get; set; }
    public int MaxPlayers { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Players { get; set; }
    public bool IsOpen { get; set; } = true;
    public bool Started { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }

    // Host is always the first member. Join/leave set Players = Members.Count; a game-created lobby keeps
    // only the host here and its game reports Players through PATCH as before. Stored as one comma list.
    public List<int> Members { get; set; } = [];

    /// <summary>Changed on every save, so two players joining the last slot at once cannot both get in.</summary>
    public Guid Version { get; set; }
}
