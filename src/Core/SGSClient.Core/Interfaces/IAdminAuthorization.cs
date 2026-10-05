using System.Security.Claims;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;

namespace SGSClient.Core.Interfaces;

public interface IAdminAuthorization
{
    /// <summary>
    /// When Azure is not configured, always true (local dev fallback).
    /// Otherwise the stored role must be at least <see cref="UserRole.Admin"/>,
    /// or the email must match <see cref="AdminSettings.Emails"/>.
    /// </summary>
    bool IsAdmin(ClaimsPrincipal? user, string? emailOverride = null, UserRole storedRole = UserRole.User);

    /// <summary>Owner emails from configuration; they are always super admins and cannot be demoted.</summary>
    bool IsBootstrapEmail(string? email);

    /// <summary>Effective role: the stored one, raised to super admin only for bootstrap owners.</summary>
    UserRole ResolveRole(UserRole storedRole, string? email);
}
