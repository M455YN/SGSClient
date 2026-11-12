using Microsoft.EntityFrameworkCore;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Persistence.Entities;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class LocalLibraryService(
    SgsClientDbContext db,
    IAuthSession? auth = null,
    ISgsApiService? api = null,
    ICatalogCacheStore? cache = null) : ILocalLibraryStore
{
    public async Task<IReadOnlyList<GameLibraryItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await db.Installs
            .AsNoTracking()
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<GameLibraryItem>> GetOwnedAsync(CancellationToken cancellationToken = default)
    {
        var all = await GetAllAsync(cancellationToken);
        var owned = await GetOwnedGameIdsAsync(cancellationToken);
        return owned is null ? all : all.Where(x => owned.Contains(x.GameId)).ToList();
    }

    /// <summary>Null when installs are not tied to an account (sign-in not configured, or nothing known yet).</summary>
    private async Task<HashSet<int>?> GetOwnedGameIdsAsync(CancellationToken cancellationToken)
    {
        if (auth is not { IsAvailable: true } || api is null)
        {
            return null;
        }

        if (!auth.IsLoggedIn || auth.CurrentUser is not { } user)
        {
            return [];
        }

        var remote = await api.TryGetMyLibraryAsync(cancellationToken);
        if (remote is not null)
        {
            await TrySaveAccountLibraryAsync(user.AzureSub, remote, cancellationToken);
            return remote.Select(g => g.Id).ToHashSet();
        }

        if (cache is null)
        {
            return null;
        }

        // shortcut: an account with no saved list sees every install while the API is unreachable, so offline play keeps working.
        var saved = await cache.LoadAccountLibraryAsync(user.AzureSub, cancellationToken);
        return saved.SavedAtUtc is null ? null : saved.Games.Select(g => g.Id).ToHashSet();
    }

    private async Task TrySaveAccountLibraryAsync(
        string accountSub,
        IReadOnlyList<SGSClient.Core.DTOs.GameDto> games,
        CancellationToken cancellationToken)
    {
        if (cache is null)
        {
            return;
        }

        try
        {
            await cache.SaveAccountLibraryAsync(accountSub, games, cancellationToken);
        }
        catch
        {
            // cache write is best-effort
        }
    }

    public async Task<GameLibraryItem?> GetByGameIdAsync(int gameId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Installs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.GameId == gameId, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task<GameLibraryItem?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var entity = await db.Installs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Symbol == symbol, cancellationToken);

        return entity is null ? null : ToModel(entity);
    }

    public async Task UpsertAsync(GameLibraryItem item, CancellationToken cancellationToken = default)
    {
        var entity = await db.Installs.FirstOrDefaultAsync(x => x.GameId == item.GameId, cancellationToken);
        var stateChanged = entity is null
            ? item.InstallState == InstallState.Installed
            : entity.InstallState != item.InstallState;
        if (entity is null)
        {
            db.Installs.Add(new LocalInstallEntity
            {
                GameId = item.GameId,
                BuildId = item.BuildId,
                Symbol = item.Symbol,
                Title = item.Title,
                Platform = item.Platform,
                Architecture = item.Architecture,
                InstallState = item.InstallState,
                InstallPath = item.InstallPath,
                ExecutablePath = item.ExecutablePath,
                InstalledVersion = item.InstalledVersion,
                TargetVersion = item.TargetVersion,
                CoverUrl = item.CoverUrl,
                LogoUrl = item.LogoUrl,
                CustomCoverPath = item.CustomCoverPath,
                LastPlayedUtc = item.LastPlayedUtc
            });
        }
        else
        {
            entity.BuildId = item.BuildId;
            entity.Symbol = item.Symbol;
            entity.Title = item.Title;
            entity.Platform = item.Platform;
            entity.Architecture = item.Architecture;
            entity.InstallState = item.InstallState;
            entity.InstallPath = item.InstallPath;
            entity.ExecutablePath = item.ExecutablePath;
            entity.InstalledVersion = item.InstalledVersion;
            entity.TargetVersion = item.TargetVersion;
            entity.CoverUrl = item.CoverUrl;
            entity.LogoUrl = item.LogoUrl;
            entity.CustomCoverPath = item.CustomCoverPath;
            entity.LastPlayedUtc = item.LastPlayedUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (stateChanged)
        {
            InstalledGamesNotifier.Raise();
        }
    }

    public async Task RemoveAsync(int gameId, CancellationToken cancellationToken = default)
    {
        var entities = await db.Installs.Where(x => x.GameId == gameId).ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return;
        }

        db.Installs.RemoveRange(entities);
        await db.SaveChangesAsync(cancellationToken);
        InstalledGamesNotifier.Raise();
    }

    private static GameLibraryItem ToModel(LocalInstallEntity entity) => new()
    {
        GameId = entity.GameId,
        BuildId = entity.BuildId,
        Symbol = entity.Symbol,
        Title = entity.Title,
        Platform = entity.Platform,
        Architecture = entity.Architecture,
        InstallState = entity.InstallState,
        InstallPath = entity.InstallPath,
        ExecutablePath = entity.ExecutablePath,
        InstalledVersion = entity.InstalledVersion,
        TargetVersion = entity.TargetVersion,
        CoverUrl = entity.CoverUrl,
        LogoUrl = entity.LogoUrl,
        CustomCoverPath = entity.CustomCoverPath,
        LastPlayedUtc = entity.LastPlayedUtc
    };
}
