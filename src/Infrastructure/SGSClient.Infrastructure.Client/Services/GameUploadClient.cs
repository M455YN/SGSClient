using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

public interface IGameUploadClient
{
    Task<GamePackagePublishResult> UploadAsync(
        string zipPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class GameUploadClient(HttpClient http, IAuthSession auth) : IGameUploadClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GamePackagePublishResult> UploadAsync(
        string zipPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipPath))
        {
            return GamePackagePublishResult.Fail("Brak pliku paczki do wysłania.");
        }

        using var content = new MultipartFormDataContent();
        await using var stream = File.OpenRead(zipPath);
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(streamContent, "package", Path.GetFileName(zipPath));
        progress?.Report(0.05);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/Games/uploads")
        {
            Content = content
        };

        var token = await auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        progress?.Report(1);

        if (response.IsSuccessStatusCode)
        {
            var game = await response.Content.ReadFromJsonAsync<GameDto>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return game is null
                ? GamePackagePublishResult.Fail("Serwer nie zwrócił danych gry.")
                : GamePackagePublishResult.Ok(game);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (TryReadErrors(body, out var errors))
        {
            return GamePackagePublishResult.Fail(errors);
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return GamePackagePublishResult.Fail("Musisz być zalogowany, żeby opublikować grę. Wejdź w Profil i zaloguj się.");
        }

        return GamePackagePublishResult.Fail(
            string.IsNullOrWhiteSpace(body)
                ? $"Upload nieudany ({(int)response.StatusCode})."
                : body);
    }

    private static bool TryReadErrors(string body, out string[] errors)
    {
        errors = [];
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                errors = arr.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToArray();
                return errors.Length > 0;
            }
        }
        catch
        {
            // not JSON problem payload
        }

        return false;
    }
}
