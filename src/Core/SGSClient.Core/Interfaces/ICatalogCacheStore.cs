namespace SGSClient.Core.Interfaces;

public interface ICatalogCacheStore
{
    Task SaveCatalogAsync(IReadOnlyList<SGSClient.Core.DTOs.GameDto> games, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SGSClient.Core.DTOs.GameDto> Games, DateTimeOffset? SavedAtUtc)> LoadCatalogAsync(
        CancellationToken cancellationToken = default);

    Task SaveWishlistAsync(IReadOnlyList<SGSClient.Core.DTOs.GameDto> games, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SGSClient.Core.DTOs.GameDto> Games, DateTimeOffset? SavedAtUtc)> LoadWishlistAsync(
        CancellationToken cancellationToken = default);
}
