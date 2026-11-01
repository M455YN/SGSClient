using System.Security.Claims;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Api.Auth;

/// <summary>Who is calling: the signed-in user, or local user 1 when Azure AD is disabled (development only).</summary>
public static class CurrentUser
{
    public static (string Sub, string DisplayName, string? Email)? ReadClaims(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var sub = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return null;
        }

        var displayName = principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Name) ?? "Player";
        return (sub, displayName, EmailClaimHelper.FromPrincipal(principal));
    }

    public static async Task<UserProfileDto?> ResolveProfileAsync(
        this IUserService users,
        ClaimsPrincipal? principal,
        AzureAdSettings azure,
        CancellationToken cancellationToken = default)
    {
        if (!azure.IsConfigured)
        {
            return await users.GetByIdAsync(1, cancellationToken)
                ?? await users.UpsertFromClaimsAsync("local-dev", "Player", null, null, cancellationToken);
        }

        return ReadClaims(principal) is { } claims
            ? await users.UpsertFromClaimsAsync(claims.Sub, claims.DisplayName, claims.Email, avatarUrl: null, cancellationToken)
            : null;
    }

    public static async Task<int?> ResolveUserIdAsync(
        this IUserService users,
        ClaimsPrincipal? principal,
        AzureAdSettings azure,
        CancellationToken cancellationToken = default) =>
        (await users.ResolveProfileAsync(principal, azure, cancellationToken))?.Id;
}
