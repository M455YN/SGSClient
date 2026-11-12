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

    /// <summary>Open lobbies with a free slot that the user's friends host.</summary>
    Task<IReadOnlyList<LobbyDto>> ListFriendLobbiesAsync(int userId, CancellationToken cancellationToken = default);
}
