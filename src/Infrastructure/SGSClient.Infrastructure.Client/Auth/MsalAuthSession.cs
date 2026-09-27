using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Auth;

public sealed class MsalAuthSession : IAuthSession
{
    private readonly AzureAdSettings _settings;
    private readonly Func<IUserApiClient> _userApiFactory;
    private readonly IPublicClientApplication? _msal;
    private AuthenticationResult? _authResult;

    public MsalAuthSession(IOptions<AzureAdSettings> options, Func<IUserApiClient> userApiFactory)
    {
        _settings = options.Value;
        _userApiFactory = userApiFactory;

        if (!_settings.IsConfigured)
        {
            return;
        }

        _msal = PublicClientApplicationBuilder
            .Create(_settings.ClientId)
            .WithB2CAuthority(_settings.Authority)
            .WithRedirectUri("http://localhost")
            .Build();

        MsalTokenCacheHelper.EnableSerialization(_msal.UserTokenCache);
    }

    public bool IsAvailable => _msal is not null;
    public bool IsLoggedIn => _authResult is not null && CurrentUser is not null;
    public UserProfileDto? CurrentUser { get; private set; }
    public string? LastError { get; private set; }

    public async Task<bool> TrySilentLoginAsync(CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (_msal is null)
        {
            LastError = "Azure AD B2C is disabled. Set AzureAd:Enabled, ClientId and TenantName in appsettings.";
            return false;
        }

        var accounts = await _msal.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();
        if (account is null)
        {
            return false;
        }

        try
        {
            _authResult = await _msal
                .AcquireTokenSilent(_settings.Scopes, account)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);

            return await LoadProfileAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MsalUiRequiredException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _authResult = null;
            CurrentUser = null;
            LastError = ex.Message;
            return false;
        }
    }

    public Task<bool> LoginAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) =>
        InteractiveAsync(parentWindowHandle, signUp: false, cancellationToken);

    public Task<bool> SignUpAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) =>
        InteractiveAsync(parentWindowHandle, signUp: true, cancellationToken);

    private async Task<bool> InteractiveAsync(nint parentWindowHandle, bool signUp, CancellationToken cancellationToken)
    {
        LastError = null;
        if (_msal is null)
        {
            LastError = "Azure AD B2C is disabled. Set AzureAd:Enabled, ClientId and TenantName in appsettings.";
            return false;
        }

        try
        {
            var accounts = await _msal.GetAccountsAsync().ConfigureAwait(false);
            foreach (var account in accounts)
            {
                await _msal.RemoveAsync(account).ConfigureAwait(false);
            }

            var request = _msal.AcquireTokenInteractive(_settings.Scopes);
            if (signUp)
            {
                request = request
                    .WithB2CAuthority(_settings.SignUpAuthority)
                    .WithPrompt(Prompt.ForceLogin);
            }

            if (parentWindowHandle != 0)
            {
                request = request.WithParentActivityOrWindow(parentWindowHandle);
            }

            _authResult = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return await LoadProfileAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "access_denied" && ex.Message.Contains("AADB2C90118", StringComparison.Ordinal))
        {
            return await LoginWithPasswordResetAsync(parentWindowHandle, cancellationToken).ConfigureAwait(false);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "access_denied" && ex.Message.Contains("AADB2C90091", StringComparison.Ordinal))
        {
            return await LoginAsync(parentWindowHandle, cancellationToken).ConfigureAwait(false);
        }
        catch (MsalClientException ex) when (ex.ErrorCode is "authentication_canceled" or "access_denied")
        {
            _authResult = null;
            CurrentUser = null;
            LastError = "Sign-in cancelled";
            return false;
        }
        catch (Exception ex)
        {
            _authResult = null;
            CurrentUser = null;
            LastError = ex.Message;
            return false;
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        LastError = null;
        if (_msal is not null)
        {
            var accounts = await _msal.GetAccountsAsync().ConfigureAwait(false);
            foreach (var account in accounts)
            {
                await _msal.RemoveAsync(account).ConfigureAwait(false);
            }
        }

        _authResult = null;
        CurrentUser = null;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void SetCurrentUser(UserProfileDto? user) => CurrentUser = user;

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_msal is null)
        {
            return null;
        }

        if (_authResult is not null && _authResult.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return PreferApiBearerToken(_authResult);
        }

        var accounts = await _msal.GetAccountsAsync().ConfigureAwait(false);
        var account = accounts.FirstOrDefault();
        if (account is null)
        {
            return null;
        }

        try
        {
            _authResult = await _msal
                .AcquireTokenSilent(_settings.Scopes, account)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(false);
            return PreferApiBearerToken(_authResult);
        }
        catch (MsalUiRequiredException)
        {
            return null;
        }
    }

    private async Task<bool> LoginWithPasswordResetAsync(nint parentWindowHandle, CancellationToken cancellationToken)
    {
        if (_msal is null || string.IsNullOrWhiteSpace(_settings.PasswordResetPolicy))
        {
            LastError = "Password reset requested, but PasswordResetPolicy is not configured.";
            return false;
        }

        try
        {
            var request = _msal
                .AcquireTokenInteractive(_settings.Scopes)
                .WithB2CAuthority(_settings.PasswordResetAuthority)
                .WithPrompt(Prompt.SelectAccount);

            if (parentWindowHandle != 0)
            {
                request = request.WithParentActivityOrWindow(parentWindowHandle);
            }

            _authResult = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return await LoadProfileAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MsalServiceException ex) when (ex.ErrorCode == "access_denied" && ex.Message.Contains("AADB2C90091", StringComparison.Ordinal))
        {
            return await LoginAsync(parentWindowHandle, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _authResult = null;
            CurrentUser = null;
            LastError = ex.Message;
            return false;
        }
    }

    private async Task<bool> LoadProfileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var api = _userApiFactory();
            CurrentUser = await api.GetMeAsync(cancellationToken).ConfigureAwait(false);
            var fromIdToken = TryParseProfileFromIdToken(_authResult);
            fromIdToken = EnrichEmailFromAccount(fromIdToken, _authResult);

            if (CurrentUser is not null)
            {
                CurrentUser = await EnsureEmailSyncedAsync(api, CurrentUser, fromIdToken, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            CurrentUser = fromIdToken;
            if (CurrentUser is not null)
            {
                LastError = "Signed in locally, but API /api/me failed. Start Api with the same AzureAd settings.";
                return true;
            }

            LastError = "Token acquired, but profile could not be loaded.";
            return false;
        }
        catch (Exception ex)
        {
            CurrentUser = TryParseProfileFromIdToken(_authResult);
            if (CurrentUser is not null)
            {
                LastError = $"API /api/me failed: {ex.Message}";
                return true;
            }

            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// B2C often puts email only in the ID token, not the access token used by /api/me.
    /// Sync it to the profile so admin checks and UI can see the address.
    /// </summary>
    private static async Task<UserProfileDto> EnsureEmailSyncedAsync(
        IUserApiClient api,
        UserProfileDto profile,
        UserProfileDto? fromIdToken,
        CancellationToken cancellationToken)
    {
        var idEmail = EmailClaimHelper.Normalize(fromIdToken?.Email);
        var profileEmail = EmailClaimHelper.Normalize(profile.Email);

        if (profileEmail is not null)
        {
            return string.Equals(profile.Email, profileEmail, StringComparison.Ordinal)
                ? profile
                : CloneProfile(profile, profileEmail, profile.IsAdmin);
        }

        if (idEmail is null)
        {
            return profile;
        }

        var updated = await api.UpdateMeAsync(
                new UpdateProfileRequestDto { Email = idEmail },
                cancellationToken)
            .ConfigureAwait(false);

        if (updated is not null)
        {
            return updated;
        }

        // PATCH may have applied even if the body failed to deserialize — refresh IsAdmin from API.
        var refreshed = await api.GetMeAsync(cancellationToken).ConfigureAwait(false);
        if (refreshed is not null)
        {
            var refreshedEmail = EmailClaimHelper.Normalize(refreshed.Email) ?? idEmail;
            return CloneProfile(refreshed, refreshedEmail, refreshed.IsAdmin);
        }

        return CloneProfile(profile, idEmail, profile.IsAdmin);
    }

    private static UserProfileDto CloneProfile(UserProfileDto source, string? email, bool isAdmin) => new()
    {
        Id = source.Id,
        AzureSub = source.AzureSub,
        DisplayName = source.DisplayName,
        Email = email,
        AvatarUrl = source.AvatarUrl,
        Bio = source.Bio,
        Location = source.Location,
        PlayerTag = source.PlayerTag,
        CreatedAtUtc = source.CreatedAtUtc,
        LastLoginUtc = source.LastLoginUtc,
        IsAdmin = isAdmin
    };

    private static UserProfileDto? EnrichEmailFromAccount(UserProfileDto? profile, AuthenticationResult? result)
    {
        var accountEmail = EmailClaimHelper.Normalize(result?.Account?.Username);
        if (accountEmail is null)
        {
            return profile;
        }

        if (profile is null)
        {
            return new UserProfileDto
            {
                AzureSub = result?.Account?.HomeAccountId?.ObjectId ?? "unknown",
                DisplayName = result?.Account?.Username ?? "Player",
                Email = accountEmail,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
        }

        if (EmailClaimHelper.Normalize(profile.Email) is not null)
        {
            return profile;
        }

        return CloneProfile(profile, accountEmail, profile.IsAdmin);
    }

    private static UserProfileDto? TryParseProfileFromIdToken(AuthenticationResult? result)
    {
        var token = result?.IdToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            if (string.IsNullOrWhiteSpace(sub))
            {
                return null;
            }

            return new UserProfileDto
            {
                AzureSub = sub,
                DisplayName = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value ?? "Player",
                Email = SGSClient.Core.Auth.EmailClaimHelper.FromClaims(jwt.Claims),
                AvatarUrl = jwt.Claims.FirstOrDefault(c => c.Type == "picture")?.Value,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// With only openid/offline_access scopes, B2C access tokens often have the wrong audience
    /// for our API. Prefer the ID token (aud = client id) unless a dedicated ApiAudience is set.
    /// </summary>
    private string PreferApiBearerToken(AuthenticationResult result)
    {
        var useIdToken = string.IsNullOrWhiteSpace(_settings.ApiAudience)
            || !_settings.Scopes.Any(static s =>
                s.Contains("api://", StringComparison.OrdinalIgnoreCase)
                || s.Contains(".onmicrosoft.com/", StringComparison.OrdinalIgnoreCase)
                || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

        if (useIdToken)
        {
            return string.IsNullOrWhiteSpace(result.IdToken) ? result.AccessToken : result.IdToken;
        }

        return string.IsNullOrWhiteSpace(result.AccessToken) ? result.IdToken : result.AccessToken;
    }
}
