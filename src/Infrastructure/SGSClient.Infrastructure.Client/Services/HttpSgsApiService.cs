using System.Net.Http.Headers;
using System.Net.Http.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class HttpSgsApiService(HttpClient http, IAuthSession auth) : ISgsApiService
{
    public async Task<IReadOnlyList<GameDto>> GetGamesAsync(
        GameCatalogQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildGamesUrl(query);
            var games = await http.GetFromJsonAsync<List<GameDto>>(url, cancellationToken)
                .ConfigureAwait(false);
            return games ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<GameDto?> GetGameAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync($"api/Games/{id}", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<GameDlcDto>> GetGameDlcsAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync($"api/Games/{gameId}/dlcs", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var dlcs = await response.Content.ReadFromJsonAsync<List<GameDlcDto>>(cancellationToken)
                .ConfigureAwait(false);
            return dlcs ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GameDto>> GetMyGamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/me/games");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var games = await response.Content.ReadFromJsonAsync<List<GameDto>>(cancellationToken)
                .ConfigureAwait(false);
            return games ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GameDto>> GetMyLibraryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/me/library");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var games = await response.Content.ReadFromJsonAsync<List<GameDto>>(cancellationToken)
                .ConfigureAwait(false);
            return games ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<GameDto?> ClaimGameAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/me/library/{gameId}");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RemoveFromLibraryAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/me/library/{gameId}");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<GameDto>> GetWishlistAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/me/wishlist");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var games = await response.Content.ReadFromJsonAsync<List<GameDto>>(cancellationToken)
                .ConfigureAwait(false);
            return games ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<GameDto?> AddToWishlistAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/me/wishlist/{gameId}");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> RemoveFromWishlistAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/me/wishlist/{gameId}");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendPresenceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/me/presence");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<GameDto?> UpdateGameAsync(
        int gameId,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Patch, $"api/Games/{gameId}")
            {
                Content = JsonContent.Create(request)
            };
            await AttachBearerAsync(message, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildGamesUrl(GameCatalogQuery? query)
    {
        if (query is null)
        {
            return "api/Games";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            parts.Add($"q={Uri.EscapeDataString(query.Q.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            parts.Add($"genre={Uri.EscapeDataString(query.Genre.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            parts.Add($"tag={Uri.EscapeDataString(query.Tag.Trim())}");
        }

        if (query.ReleaseStatus is { } status)
        {
            parts.Add($"releaseStatus={(int)status}");
        }

        if (query.Featured is { } featured)
        {
            parts.Add($"featured={featured.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrWhiteSpace(query.Sort))
        {
            parts.Add($"sort={Uri.EscapeDataString(query.Sort.Trim())}");
        }

        return parts.Count == 0 ? "api/Games" : "api/Games?" + string.Join('&', parts);
    }

    public async Task<bool> DeleteGameAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/Games/{gameId}");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<GameDto>> GetAdminGamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/games");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var games = await response.Content.ReadFromJsonAsync<List<GameDto>>(cancellationToken)
                .ConfigureAwait(false);
            return games ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<GameDto?> UpdateAdminGameAsync(
        int gameId,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Patch, $"api/admin/games/{gameId}")
            {
                Content = JsonContent.Create(request)
            };
            await AttachBearerAsync(message, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteAdminGameAsync(int gameId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/admin/games/{gameId}");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/admin/users");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var users = await response.Content.ReadFromJsonAsync<List<AdminUserDto>>(cancellationToken)
                .ConfigureAwait(false);
            return users ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<AdminRoleUpdateResult> UpdateAdminUserRoleAsync(
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Patch, $"api/admin/users/{userId}/role")
            {
                Content = JsonContent.Create(new UpdateUserRoleRequestDto { Role = role })
            };
            await AttachBearerAsync(message, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new AdminRoleUpdateResult
                {
                    Error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false)
                };
            }

            var updated = await response.Content.ReadFromJsonAsync<AdminUserDto>(cancellationToken)
                .ConfigureAwait(false);
            return updated is null
                ? new AdminRoleUpdateResult { Error = "Serwer nie zwrócił zaktualizowanego użytkownika." }
                : new AdminRoleUpdateResult { User = updated };
        }
        catch (Exception ex)
        {
            return new AdminRoleUpdateResult { Error = ex.Message };
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiErrorPayload>(cancellationToken)
                .ConfigureAwait(false);
            if (payload?.Errors is { Length: > 0 } errors)
            {
                return string.Join(" ", errors);
            }
        }
        catch
        {
            // fall through to the status code
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Zaloguj się ponownie.",
            System.Net.HttpStatusCode.Forbidden => "Brak uprawnień do tej operacji.",
            System.Net.HttpStatusCode.NotFound => "Nie znaleziono użytkownika.",
            _ => $"Błąd serwera ({(int)response.StatusCode})."
        };
    }

    private sealed class ApiErrorPayload
    {
        public string[]? Errors { get; init; }
    }

    public Task<GameDto?> UploadGameCoverAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/cover" : $"api/Games/{gameId}/media/cover",
            HttpMethod.Put,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> ClearGameCoverAsync(
        int gameId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/cover" : $"api/Games/{gameId}/media/cover",
            HttpMethod.Delete,
            cancellationToken);

    public Task<GameDto?> UploadGameHeaderAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/header" : $"api/Games/{gameId}/media/header",
            HttpMethod.Put,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> ClearGameHeaderAsync(
        int gameId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/header" : $"api/Games/{gameId}/media/header",
            HttpMethod.Delete,
            cancellationToken);

    public Task<GameDto?> UploadGameIntroAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/intro" : $"api/Games/{gameId}/media/intro",
            HttpMethod.Put,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> ClearGameIntroAsync(
        int gameId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/intro" : $"api/Games/{gameId}/media/intro",
            HttpMethod.Delete,
            cancellationToken);

    public Task<GameDto?> AddGameScreenshotAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/screenshots" : $"api/Games/{gameId}/media/screenshots",
            HttpMethod.Post,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> RemoveGameScreenshotAsync(
        int gameId,
        int screenshotId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin
                ? $"api/admin/games/{gameId}/media/screenshots/{screenshotId}"
                : $"api/Games/{gameId}/media/screenshots/{screenshotId}",
            HttpMethod.Delete,
            cancellationToken);

    public async Task<IReadOnlyList<GameBuildVersionDto>> GetBuildVersionsAsync(
        int gameId,
        int buildId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http
                .GetAsync($"api/Games/{gameId}/builds/{buildId}/versions", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var versions = await response.Content
                .ReadFromJsonAsync<List<GameBuildVersionDto>>(cancellationToken)
                .ConfigureAwait(false);
            return versions ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GameBuildVersionDto>> GetOwnedVersionsAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/Games/{gameId}/studio/versions");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var versions = await response.Content
                .ReadFromJsonAsync<List<GameBuildVersionDto>>(cancellationToken)
                .ConfigureAwait(false);
            return versions ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<GameStatsDto?> GetOwnedGameStatsAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/Games/{gameId}/stats");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameStatsDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<GameBuildVersionDto?> UpdateVersionNotesAsync(
        int gameId,
        int versionId,
        string? releaseNotes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Patch,
                $"api/Games/{gameId}/versions/{versionId}/notes")
            {
                Content = JsonContent.Create(new UpdateVersionNotesRequestDto { ReleaseNotes = releaseNotes })
            };
            await AttachBearerAsync(message, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameBuildVersionDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<GameBuildVersionDto>> GetAdminVersionsAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/admin/games/{gameId}/versions");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var versions = await response.Content
                .ReadFromJsonAsync<List<GameBuildVersionDto>>(cancellationToken)
                .ConfigureAwait(false);
            return versions ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<GameStatsDto?> GetAdminGameStatsAsync(
        int gameId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/admin/games/{gameId}/stats");
            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameStatsDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<GameBuildVersionDto?> UpdateAdminVersionNotesAsync(
        int gameId,
        int versionId,
        string? releaseNotes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Patch,
                $"api/admin/games/{gameId}/versions/{versionId}/notes")
            {
                Content = JsonContent.Create(new UpdateVersionNotesRequestDto { ReleaseNotes = releaseNotes })
            };
            await AttachBearerAsync(message, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameBuildVersionDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task AttachBearerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (auth.IsAvailable && auth.IsLoggedIn)
        {
            var token = await auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
    }

    private async Task<GameDto?> UploadMediaFileAsync(
        string url,
        HttpMethod method,
        string localFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(localFilePath);
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(stream);
            content.Add(fileContent, "file", Path.GetFileName(localFilePath));

            using var message = new HttpRequestMessage(method, url) { Content = content };
            await AttachBearerAsync(message, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task<GameDto?> SendMediaAsync(
        string url,
        HttpMethod method,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = new HttpRequestMessage(method, url);
            await AttachBearerAsync(message, cancellationToken).ConfigureAwait(false);
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
