using System.Security.Claims;
using System.Text.Json;

namespace SGSClient.Core.Auth;

/// <summary>Normalizes Azure AD B2C email claims (string or JSON array).</summary>
public static class EmailClaimHelper
{
    public static string? FromPrincipal(ClaimsPrincipal? user)
    {
        if (user?.Claims is null)
        {
            return null;
        }

        return FromClaims(user.Claims);
    }

    public static string? FromClaims(IEnumerable<Claim> claims)
    {
        foreach (var claim in claims)
        {
            if (!IsEmailClaimType(claim.Type))
            {
                continue;
            }

            var normalized = Normalize(claim.Value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            try
            {
                var list = JsonSerializer.Deserialize<string[]>(value);
                value = list?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e))?.Trim() ?? string.Empty;
            }
            catch (JsonException)
            {
                // fall through
            }
        }

        value = value.Trim().Trim('"');
        return string.IsNullOrWhiteSpace(value) || !value.Contains('@') ? null : value;
    }

    private static bool IsEmailClaimType(string type) =>
        type is "emails" or "email" or "otherMails" or "signInNames.emailAddress"
        || type.Equals(ClaimTypes.Email, StringComparison.OrdinalIgnoreCase)
        || type.EndsWith("/emailaddress", StringComparison.OrdinalIgnoreCase)
        || type.EndsWith("/emails", StringComparison.OrdinalIgnoreCase)
        || type.Contains("email", StringComparison.OrdinalIgnoreCase);
}
