using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface ISgsApiService
{
    Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken = default);
    Task<GameDto?> GetGameAsync(int id, CancellationToken cancellationToken = default);
}
