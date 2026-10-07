using System.Security.Claims;
using Microsoft.Extensions.Options;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Server.Services;

public sealed class AdminAuthorization(
    IOptions<AdminSettings> adminOptions,
    IOptions<AzureAdSettings> azureOptions) : IAdminAuthorization
{
    public bool IsAdmin(ClaimsPrincipal? user, string? emailOverride = null, UserRole storedRole = UserRole.User)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            return true;
        }

        var email = EmailClaimHelper.Normalize(emailOverride)
            ?? EmailClaimHelper.FromPrincipal(user);

        return ResolveRole(storedRole, email) >= UserRole.Admin;
    }

    public bool IsSuperAdmin(ClaimsPrincipal? user, string? emailOverride = null, UserRole storedRole = UserRole.User)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            return true;
        }

        var email = EmailClaimHelper.Normalize(emailOverride)
            ?? EmailClaimHelper.FromPrincipal(user);

        return ResolveRole(storedRole, email) >= UserRole.SuperAdmin;
    }

    public bool IsBootstrapEmail(string? email)
    {
        var normalized = EmailClaimHelper.Normalize(email);
        if (normalized is null)
        {
            return false;
        }

        var allowed = adminOptions.Value.Emails ?? [];
        return allowed.Any(a =>
            EmailClaimHelper.Normalize(a) is { } allowedEmail
            && string.Equals(allowedEmail, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public UserRole ResolveRole(UserRole storedRole, string? email)
    {
        // Only bootstrap owners are forced to SuperAdmin. Do not elevate everyone when
        // Azure is off — that masked stored roles and made admin role edits look like
        // they never saved (DB updated, response always came back as SuperAdmin).
        if (IsBootstrapEmail(email))
        {
            return UserRole.SuperAdmin;
        }

        return storedRole;
    }
}
