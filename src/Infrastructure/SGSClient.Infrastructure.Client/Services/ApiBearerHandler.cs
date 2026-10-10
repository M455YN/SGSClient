using System.Net.Http.Headers;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

/// <summary>
/// Adds the signed-in user's bearer token to requests sent to the SGS API host.
/// Requests to any other host (CDN, storage) never receive the token.
/// </summary>
public sealed class ApiBearerHandler(IAuthSession auth, Uri apiBaseAddress) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null
            && auth is { IsAvailable: true, IsLoggedIn: true }
            && IsApiRequest(request.RequestUri))
        {
            var token = await auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private bool IsApiRequest(Uri? uri) =>
        uri is null
        || !uri.IsAbsoluteUri
        || (uri.Scheme == apiBaseAddress.Scheme
            && string.Equals(uri.Host, apiBaseAddress.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == apiBaseAddress.Port);
}
