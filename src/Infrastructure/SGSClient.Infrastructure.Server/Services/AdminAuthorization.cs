using System.Security.Claims;
using Microsoft.Extensions.Options;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Server.Services;

public sealed class AdminAuthorization(
    IOptions<AdminSettings> adminOptions,
    IOptions<AzureAdSettings> azureOptions) : IAdminAuthorization
{
    public bool IsAdmin(ClaimsPrincipal? user, string? emailOverride = null)
    {
        if (!azureOptions.Value.IsConfigured)
        {
            return true;
        }

        var email = EmailClaimHelper.Normalize(emailOverride)
            ?? EmailClaimHelper.FromPrincipal(user);

        return IsAdminEmail(email);
    }

    public bool IsAdminEmail(string? email)
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
}
