namespace SGSClient.Core.DTOs;

/// <summary>A game room a host opened so friends can find the host's address by a short code.</summary>
public sealed class LobbyDto
{
    public required string Code { get; init; }
    public int GameId { get; init; }
    public string? GameTitle { get; init; }
    public int HostUserId { get; init; }
    public string? HostName { get; init; }

    /// <summary>Where the host listens, e.g. <c>192.168.0.5:7777</c>. The game decides how to connect.</summary>
    public required string Address { get; init; }

    public int Players { get; init; }
    public int MaxPlayers { get; init; }
    public bool IsOpen { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class CreateLobbyRequest
{
    /// <summary>Filled in by the client bridge from the launched game; the game does not send it.</summary>
    public int GameId { get; set; }

    public string Address { get; set; } = string.Empty;
    public int MaxPlayers { get; set; } = 4;
    public int Players { get; set; } = 1;
}

/// <summary>Only the fields that are present change; any call also keeps the lobby alive.</summary>
public sealed class UpdateLobbyRequest
{
    public string? Address { get; set; }
    public int? Players { get; set; }
    public bool? IsOpen { get; set; }
}

public sealed class InviteLobbyRequest
{
    public int FriendUserId { get; set; }
}
