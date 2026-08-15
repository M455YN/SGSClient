using System.Net.Http.Headers;
using System.Net.Http.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class HttpSgsApiService(HttpClient http, IAuthSession auth) : ISgsApiService
{
    public async Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var games = await http.GetFromJsonAsync<List<GameDto>>("api/Games", cancellationToken)
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
}
