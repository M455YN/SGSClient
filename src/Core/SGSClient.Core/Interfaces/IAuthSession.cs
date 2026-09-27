using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IAuthSession
{
    /// <summary>True when Azure AD B2C is enabled and ClientId/Tenant/policy are set.</summary>
    bool IsAvailable { get; }

    bool IsLoggedIn { get; }
    UserProfileDto? CurrentUser { get; }

    /// <summary>Last failure reason for UI (not configured, cancelled, API error, …).</summary>
    string? LastError { get; }

    Task<bool> TrySilentLoginAsync(CancellationToken cancellationToken = default);
    Task<bool> LoginAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default);
    Task<bool> SignUpAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    void SetCurrentUser(UserProfileDto? user);
}
