using SGSClient.Core.Models;

namespace SGSClient.Core.Interfaces;

public interface ILocalLibraryStore
{
    Task<IReadOnlyList<GameLibraryItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GameLibraryItem?> GetByGameIdAsync(int gameId, CancellationToken cancellationToken = default);
    Task<GameLibraryItem?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);
    Task UpsertAsync(GameLibraryItem item, CancellationToken cancellationToken = default);
    Task RemoveAsync(int gameId, CancellationToken cancellationToken = default);
}
