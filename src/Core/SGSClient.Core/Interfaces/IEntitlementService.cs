using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IEntitlementService
{
    Task<bool> HasAsync(int userId, int gameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameDto>> ListAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims a published game for free. Idempotent (returns existing entitlement game).
    /// Throws <see cref="KeyNotFoundException"/> when the game is missing/unpublished,
    /// <see cref="InvalidOperationException"/> when DLC parent entitlement is missing.
    /// </summary>
    Task<GameDto> ClaimFreeAsync(int userId, int gameId, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(int userId, int gameId, CancellationToken cancellationToken = default);

    /// <summary>True when the user may download packages for the game (entitlement, owner, or admin).</summary>
    Task<bool> CanDownloadPackageAsync(
        int userId,
        string symbol,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
