using System.Security.Claims;
using SGSClient.Core.Configuration;

namespace SGSClient.Core.Interfaces;

public interface IAdminAuthorization
{
    /// <summary>
    /// When Azure is not configured, always true (local dev fallback).
    /// Otherwise matches claim/profile email against <see cref="AdminSettings.Emails"/>.
    /// </summary>
    bool IsAdmin(ClaimsPrincipal? user, string? emailOverride = null);

    bool IsAdminEmail(string? email);
}
