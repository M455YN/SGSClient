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

    public async Task<UserProfileDto?> UploadAvatarAsync(string localFilePath, CancellationToken cancellationToken = default)
    {
        var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(localFilePath);
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(stream);
            content.Add(fileContent, "file", Path.GetFileName(localFilePath));

            using var request = new HttpRequestMessage(HttpMethod.Put, "api/me/avatar") { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<UserProfileDto>(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public Task<UserProfileDto?> ClearAvatarAsync(CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Delete, "api/me/avatar", body: null, cancellationToken);

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
