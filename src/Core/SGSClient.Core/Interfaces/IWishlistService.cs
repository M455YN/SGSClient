using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IWishlistService
{
    Task<IReadOnlyList<GameDto>> ListAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Adds a published game. Idempotent. Returns null when the game is missing/unpublished.</summary>
    Task<GameDto?> AddAsync(int userId, int gameId, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(int userId, int gameId, CancellationToken cancellationToken = default);
}
