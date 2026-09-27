using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SGSClient.Core.Auth;
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

    private async Task<int?> ResolveUserIdAsync()
    {
        var azure = azureOptions.Value;
        if (!azure.IsConfigured)
        {
            var local = await users.GetByIdAsync(1);
            if (local is not null)
            {
                return local.Id;
            }

            var created = await users.UpsertFromClaimsAsync("local-dev", "Player", null, null);
            return created.Id;
        }

        if (Context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var sub = Context.User.FindFirstValue("sub")
            ?? Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return null;
        }

        var displayName = Context.User.FindFirstValue("name")
            ?? Context.User.FindFirstValue(ClaimTypes.Name)
            ?? "Player";
        var email = EmailClaimHelper.FromPrincipal(Context.User);
        var profile = await users.UpsertFromClaimsAsync(sub, displayName, email, avatarUrl: null);
        return profile.Id;
    }
}
