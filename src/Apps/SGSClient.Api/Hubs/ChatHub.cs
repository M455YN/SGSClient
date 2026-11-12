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
    IOptions<AzureAdSettings> azureOptions) : Hub
{
    public static string UserGroup(int userId) => $"user:{userId}";

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
