using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Auth;

public sealed class BearerUserApiClient(HttpClient http, IAuthSession authSession) : IUserApiClient
{
    public Task<UserProfileDto?> GetMeAsync(CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Get, "api/me", body: null, cancellationToken);

    public Task<UserProfileDto?> UpdateMeAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Patch, "api/me", request, cancellationToken);

    public async Task<(UserProfileDto? Profile, string? Error)> UploadAvatarAsync(
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
        {
            return (null, "Nie można odczytać wybranego pliku.");
        }

        await using var stream = File.OpenRead(localFilePath);
        return await UploadAvatarAsync(stream, Path.GetFileName(localFilePath), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(UserProfileDto? Profile, string? Error)> UploadAvatarAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, "Brak tokena — zaloguj się ponownie.");
        }

        if (content is null || string.IsNullOrWhiteSpace(fileName))
        {
            return (null, "Brak pliku zdjęcia.");
        }

        try
        {
            // WinRT / non-seekable streams break multipart; buffer first.
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (buffer.Length == 0)
            {
                return (null, "Wybrany plik jest pusty.");
            }

            buffer.Position = 0;

            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(buffer);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            });
            form.Add(fileContent, "file", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Put, "api/me/avatar") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var detail = TryExtractError(body);
                return (null, $"HTTP {(int)response.StatusCode}: {detail ?? response.ReasonPhrase ?? "błąd"}");
            }

            var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>(cancellationToken)
                .ConfigureAwait(false);
            return profile is null
                ? (null, "API nie zwróciło profilu.")
                : (profile, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public Task<UserProfileDto?> ClearAvatarAsync(CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Delete, "api/me/avatar", body: null, cancellationToken);

    private static string? TryExtractError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                if (errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                {
                    return errors[0].GetString();
                }

                if (errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in errors.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                        {
                            return prop.Value[0].GetString();
                        }
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("title", out var title))
            {
                return title.GetString();
            }
        }
        catch
        {
            // fall through
        }

        return body.Length > 180 ? body[..180] : body;
    }

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
