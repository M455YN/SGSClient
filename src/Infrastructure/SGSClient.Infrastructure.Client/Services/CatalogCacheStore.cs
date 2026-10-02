using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Persistence.Entities;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class CatalogCacheStore(IServiceScopeFactory scopes) : ICatalogCacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string CatalogKey = "catalog";
    private const string WishlistKey = "wishlist";

    public Task SaveCatalogAsync(IReadOnlyList<GameDto> games, CancellationToken cancellationToken = default) =>
        SaveAsync(CatalogKey, games, cancellationToken);

    public Task<(IReadOnlyList<GameDto> Games, DateTimeOffset? SavedAtUtc)> LoadCatalogAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(CatalogKey, cancellationToken);

    public Task SaveWishlistAsync(IReadOnlyList<GameDto> games, CancellationToken cancellationToken = default) =>
        SaveAsync(WishlistKey, games, cancellationToken);

    public Task<(IReadOnlyList<GameDto> Games, DateTimeOffset? SavedAtUtc)> LoadWishlistAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(WishlistKey, cancellationToken);

    private async Task SaveAsync(string key, IReadOnlyList<GameDto> games, CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
        var json = JsonSerializer.Serialize(games, JsonOptions);
        var row = await db.CachedPayloads.FirstOrDefaultAsync(x => x.Key == key, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            db.CachedPayloads.Add(new CachedPayloadEntity
            {
                Key = key,
                PayloadJson = json,
                SavedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.PayloadJson = json;
            row.SavedAtUtc = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(IReadOnlyList<GameDto> Games, DateTimeOffset? SavedAtUtc)> LoadAsync(
        string key,
        CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
        var row = await db.CachedPayloads.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken)
            .ConfigureAwait(false);
        if (row is null || string.IsNullOrWhiteSpace(row.PayloadJson))
        {
            return ([], null);
        }

        try
        {
            var games = JsonSerializer.Deserialize<List<GameDto>>(row.PayloadJson, JsonOptions) ?? [];
            return (games, row.SavedAtUtc);
        }
        catch
        {
            return ([], null);
        }
    }
}
