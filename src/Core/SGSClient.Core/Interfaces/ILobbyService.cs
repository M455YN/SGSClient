using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

/// <summary>
/// Lobbies only exist so a friend can find a host; the game connects to the host itself.
/// Errors: <see cref="KeyNotFoundException"/> (unknown/expired code or game),
/// <see cref="UnauthorizedAccessException"/> (not the host, game not owned, not a friend),
/// <see cref="ArgumentException"/> (invalid input).
/// </summary>
public interface ILobbyService
{
    /// <summary>Lobbies are dropped when the host has not updated them for this long.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public const string InviteNotificationKind = "lobby_invite";

    Task<LobbyDto> CreateAsync(int hostUserId, CreateLobbyRequest request, CancellationToken cancellationToken = default);

    Task<LobbyDto?> GetAsync(string code, CancellationToken cancellationToken = default);

    Task<LobbyDto> UpdateAsync(
        int hostUserId,
        string code,
        UpdateLobbyRequest request,
        CancellationToken cancellationToken = default);

    Task CloseAsync(int hostUserId, string code, CancellationToken cancellationToken = default);

    /// <summary>Notifies a friend of the host; the caller pushes the returned notification to the friend's open client.</summary>
    Task<NotificationDto> InviteAsync(
        int hostUserId,
        string code,
        int friendUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a friend of the host who owns the game to the lobby's members.</summary>
    Task<LobbyDto> JoinAsync(int userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Removes a member; the host leaving closes the lobby (returns null then).</summary>
    Task<LobbyDto?> LeaveAsync(int userId, string code, CancellationToken cancellationToken = default);

    /// <summary>Host only: marks the lobby started and closed to new members.</summary>
    Task<LobbyDto> StartAsync(int hostUserId, string code, CancellationToken cancellationToken = default);

    /// <summary>Host only: the host's friends who own the game and are not members yet.</summary>
    Task<IReadOnlyList<InvitableFriendDto>> ListInvitableAsync(
        int hostUserId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>Open, not started lobbies with a free slot that the user's friends host.</summary>
    /// <summary>A guest marks themselves ready (or not) to start.</summary>
    Task<LobbyDto> SetReadyAsync(int userId, string code, bool ready, CancellationToken cancellationToken = default);

    /// <summary>The host removes a guest from the lobby.</summary>
    Task<LobbyDto> KickAsync(int hostUserId, string code, int userId, CancellationToken cancellationToken = default);

    /// <summary>The host's game reports the match is over: the same party is back in the lobby, nobody ready.</summary>
    Task<LobbyDto> FinishAsync(int hostUserId, string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LobbyDto>> ListFriendLobbiesAsync(int userId, CancellationToken cancellationToken = default);
}
