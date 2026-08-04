using System.Net.Http.Headers;
using System.Net.Http.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Auth;

public sealed class BearerUserApiClient(HttpClient http, IAuthSession authSession) : IUserApiClient
{
    public Task<UserProfileDto?> GetMeAsync(CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Get, "api/me", body: null, cancellationToken);

    public Task<UserProfileDto?> UpdateMeAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Patch, "api/me", request, cancellationToken);

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return default;
        }

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
    }
}
