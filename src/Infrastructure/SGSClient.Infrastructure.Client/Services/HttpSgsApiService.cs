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
}
