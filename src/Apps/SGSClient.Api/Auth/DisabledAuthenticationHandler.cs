using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SGSClient.Api.Auth;

/// <summary>
/// Placeholder scheme used when Azure AD is disabled so [Authorize] challenges
/// return 401 instead of crashing with "No authenticationScheme was specified".
/// </summary>
public sealed class DisabledAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
