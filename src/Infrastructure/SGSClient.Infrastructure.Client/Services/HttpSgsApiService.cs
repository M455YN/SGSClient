using System.Net.Http.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class HttpSgsApiService(HttpClient http) : ISgsApiService
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
}
