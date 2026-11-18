using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SGSClient.Api.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Hubs;

/// <summary>Realtime social hub. Auth optional when Azure AD is disabled (local-dev).</summary>
[AllowAnonymous]
public sealed class ChatHub(
    IUserService users,
    ILobbyService lobbies,
    IOptions<AzureAdSettings> azureOptions) : Hub
{
    public static string UserGroup(int userId) => $"user:{userId}";

    /// <summary>Members of a lobby get <c>LobbyUpdated</c> with the <c>LobbyDto</c> here.</summary>
    public static string LobbyGroup(string code) => $"lobby:{code.Trim().ToUpperInvariant()}";

    /// <summary>Subscribes the caller to a lobby's updates; only its members may.</summary>
    public async Task JoinLobbyGroup(string code)
    {
        var userId = await ResolveUserIdAsync();
        var lobby = await lobbies.GetAsync(code);
        if (userId is null || lobby is null || lobby.Members.All(m => m.UserId != userId))
        {
            throw new HubException("Nie jesteś w tym lobby.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup(lobby.Code));
    }

    public Task LeaveLobbyGroup(string code) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup(code));

    public override async Task OnConnectedAsync()
    {
        var userId = await ResolveUserIdAsync();
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
        }

        await base.OnConnectedAsync();
    }

    private Task<int?> ResolveUserIdAsync() => users.ResolveUserIdAsync(Context.User, azureOptions.Value);
}
