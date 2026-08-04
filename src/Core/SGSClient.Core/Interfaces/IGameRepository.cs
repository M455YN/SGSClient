using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IGameRepository
{
    Task<IReadOnlyList<GameDto>> GetPublishedGamesAsync(CancellationToken cancellationToken = default);
}
