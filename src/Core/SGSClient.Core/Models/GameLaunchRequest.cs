namespace SGSClient.Core.Models;

public sealed class GameLaunchRequest
{
    public required int GameId { get; init; }
    public required string Title { get; init; }
    public required string ExecutablePath { get; init; }

    /// <summary>Lobby the game should join on start; handed over as <c>SGS_LOBBY_CODE</c>.</summary>
    public string? LobbyCode { get; init; }

    /// <summary>This player opened the lobby; handed over as <c>SGS_LOBBY_HOST=1</c> so the game hosts.</summary>
    public bool IsLobbyHost { get; init; }
}
