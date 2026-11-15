using System.Security.Claims;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;

namespace SGSClient.Core.Interfaces;

public interface IAdminAuthorization
{
    /// <summary>
    /// When Azure is not configured, always true (local dev fallback).
    /// Otherwise the stored role must be at least <see cref="UserRole.Admin"/>.
    /// </summary>
    bool IsAdmin(ClaimsPrincipal? user, string? emailOverride = null, UserRole storedRole = UserRole.User);

    /// <summary>
    /// When Azure is not configured, always true (local dev fallback).
    /// Otherwise the stored role must be at least <see cref="UserRole.SuperAdmin"/>.
    /// </summary>
    bool IsSuperAdmin(ClaimsPrincipal? user, string? emailOverride = null, UserRole storedRole = UserRole.User);

    /// <summary>Owner emails from configuration; one of them seeds the first super admin while none exists.</summary>
    bool IsBootstrapEmail(string? email);

    /// <summary>Effective role: the stored one; an email never raises it.</summary>
    UserRole ResolveRole(UserRole storedRole, string? email);
}
