using SGSClient.Core.Enums;
using SGSClient.Core.Models;

namespace SGSClient.Core.Interfaces;

public interface ILocalLibraryStore
{
    /// <summary>Everything on this machine, whichever account installed it.</summary>
    Task<IReadOnlyList<GameLibraryItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The part of <see cref="GetAllAsync"/> the signed-in account owns; use it for anything shown to the player.</summary>
    Task<IReadOnlyList<GameLibraryItem>> GetOwnedAsync(CancellationToken cancellationToken = default);
    Task<GameLibraryItem?> GetByGameIdAsync(int gameId, CancellationToken cancellationToken = default);
    Task<GameLibraryItem?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);
    Task UpsertAsync(GameLibraryItem item, CancellationToken cancellationToken = default);
    Task RemoveAsync(int gameId, CancellationToken cancellationToken = default);
}

public static class LocalLibraryStoreExtensions
{
    /// <summary>Records a newly claimed game, keeping an install another account already left on this machine.</summary>
    public static async Task UpsertClaimedAsync(
        this ILocalLibraryStore library,
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        if (await library.GetByGameIdAsync(item.GameId, cancellationToken) is not { InstallState: InstallState.Installed })
        {
            await library.UpsertAsync(item, cancellationToken);
        }
    }
}
