using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Services;

public sealed class GameMediaService(
    SgsServerDbContext db,
    IOptions<GameStorageSettings> storageOptions) : IGameMediaService
{
    public Task<GameDto?> SetCoverAsync(
        int gameId,
        Stream content,
        string fileName,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default) =>
        SetSlotAsync(gameId, content, fileName, MediaSlot.Cover, requireOwnerUserId, cancellationToken);

    public Task<GameDto?> ClearCoverAsync(
        int gameId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default) =>
        ClearSlotAsync(gameId, MediaSlot.Cover, requireOwnerUserId, cancellationToken);

    public Task<GameDto?> SetIntroAsync(
        int gameId,
        Stream content,
        string fileName,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default) =>
        SetSlotAsync(gameId, content, fileName, MediaSlot.Intro, requireOwnerUserId, cancellationToken);

    public Task<GameDto?> ClearIntroAsync(
        int gameId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default) =>
        ClearSlotAsync(gameId, MediaSlot.Intro, requireOwnerUserId, cancellationToken);

    public async Task<GameDto?> AddScreenshotAsync(
        int gameId,
        Stream content,
        string fileName,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default)
    {
        var game = await LoadGameAsync(gameId, requireOwnerUserId, cancellationToken);
        if (game is null)
        {
            return null;
        }

        if (game.Screenshots.Count >= SgsPackageRules.MaxScreenshots)
        {
            throw new ArgumentException($"Maksymalnie {SgsPackageRules.MaxScreenshots} screenów.");
        }

        var saved = await SaveFileAsync(
            game.Symbol,
            content,
            fileName,
            SgsPackageRules.AllowedImageExtensions,
            preferredStem: $"screen_{Guid.NewGuid():N}"[..8],
            cancellationToken);

        var nextOrder = game.Screenshots.Count == 0
            ? 0
            : game.Screenshots.Max(s => s.SortOrder) + 1;

        db.GameScreenshots.Add(new GameScreenshotEntity
        {
            GameId = game.Id,
            SortOrder = nextOrder,
            Url = saved.Url,
            SourcePath = saved.SafeName
        });

        await db.SaveChangesAsync(cancellationToken);
        return await ProjectAsync(game.Id, cancellationToken);
    }

    public async Task<GameDto?> RemoveScreenshotAsync(
        int gameId,
        int screenshotId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken = default)
    {
        var game = await LoadGameAsync(gameId, requireOwnerUserId, cancellationToken);
        if (game is null)
        {
            return null;
        }

        var shot = game.Screenshots.FirstOrDefault(s => s.Id == screenshotId);
        if (shot is null)
        {
            return null;
        }

        TryDeleteMediaFile(game.Symbol, shot.Url, shot.SourcePath);
        db.GameScreenshots.Remove(shot);
        await db.SaveChangesAsync(cancellationToken);
        return await ProjectAsync(game.Id, cancellationToken);
    }

    private async Task<GameDto?> SetSlotAsync(
        int gameId,
        Stream content,
        string fileName,
        MediaSlot slot,
        int? requireOwnerUserId,
        CancellationToken cancellationToken)
    {
        var game = await LoadGameAsync(gameId, requireOwnerUserId, cancellationToken);
        if (game is null)
        {
            return null;
        }

        var allowed = slot == MediaSlot.Cover
            ? SgsPackageRules.AllowedImageExtensions
            : SgsPackageRules.AllowedVideoExtensions;
        var stem = slot == MediaSlot.Cover ? "cover" : "intro";

        var previousUrl = slot == MediaSlot.Cover ? game.CoverUrl : game.IntroUrl;
        var saved = await SaveFileAsync(game.Symbol, content, fileName, allowed, stem, cancellationToken);

        if (slot == MediaSlot.Cover)
        {
            game.CoverUrl = saved.Url;
        }
        else
        {
            game.IntroUrl = saved.Url;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousUrl, saved.Url, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteMediaFile(game.Symbol, previousUrl, sourcePath: null);
        }

        return await ProjectAsync(game.Id, cancellationToken);
    }

    private async Task<GameDto?> ClearSlotAsync(
        int gameId,
        MediaSlot slot,
        int? requireOwnerUserId,
        CancellationToken cancellationToken)
    {
        var game = await LoadGameAsync(gameId, requireOwnerUserId, cancellationToken);
        if (game is null)
        {
            return null;
        }

        if (slot == MediaSlot.Cover)
        {
            TryDeleteMediaFile(game.Symbol, game.CoverUrl, sourcePath: null);
            game.CoverUrl = null;
        }
        else
        {
            TryDeleteMediaFile(game.Symbol, game.IntroUrl, sourcePath: null);
            game.IntroUrl = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ProjectAsync(game.Id, cancellationToken);
    }

    private async Task<GameEntity?> LoadGameAsync(
        int gameId,
        int? requireOwnerUserId,
        CancellationToken cancellationToken)
    {
        var query = db.Games
            .Include(g => g.Screenshots)
            .Where(g => g.Id == gameId);

        if (requireOwnerUserId is not null)
        {
            query = query.Where(g => g.UserId == requireOwnerUserId.Value);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<(string Url, string SafeName)> SaveFileAsync(
        string symbol,
        Stream content,
        string fileName,
        HashSet<string> allowedExtensions,
        string preferredStem,
        CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !allowedExtensions.Contains(ext))
        {
            throw new ArgumentException(
                $"Niedozwolone rozszerzenie '{ext}'. Dozwolone: {string.Join(", ", allowedExtensions)}.");
        }

        if (content.CanSeek)
        {
            if (content.Length > SgsPackageRules.MaxMediaFileBytes)
            {
                throw new ArgumentException($"Plik media jest za duży (max {SgsPackageRules.MaxMediaFileBytes / (1024 * 1024)} MB).");
            }
        }

        var storage = storageOptions.Value;
        var mediaRoot = Path.Combine(storage.StoragePath, symbol, "media");
        Directory.CreateDirectory(mediaRoot);

        var safeName = $"{preferredStem}{ext.ToLowerInvariant()}";
        var dest = Path.Combine(mediaRoot, safeName);

        await using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs, cancellationToken);
            if (fs.Length > SgsPackageRules.MaxMediaFileBytes)
            {
                fs.Close();
                TryDeletePath(dest);
                throw new ArgumentException($"Plik media jest za duży (max {SgsPackageRules.MaxMediaFileBytes / (1024 * 1024)} MB).");
            }
        }

        var mediaBase = storage.MediaPublicBaseUrl.TrimEnd('/');
        var url = $"{mediaBase}/{symbol}/{safeName}".Replace('\\', '/');
        return (url, safeName);
    }

    private void TryDeleteMediaFile(string symbol, string? url, string? sourcePath)
    {
        try
        {
            var storage = storageOptions.Value;
            var mediaRoot = Path.GetFullPath(Path.Combine(storage.StoragePath, symbol, "media"));
            string? fileName = sourcePath;
            if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(url))
            {
                fileName = Path.GetFileName(url.Replace('\\', '/'));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var path = Path.GetFullPath(Path.Combine(mediaRoot, fileName));
            if (!path.StartsWith(mediaRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TryDeletePath(path);
        }
        catch
        {
            // best-effort
        }
    }

    private static void TryDeletePath(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private async Task<GameDto?> ProjectAsync(int gameId, CancellationToken cancellationToken)
    {
        return await db.Games.AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => new GameDto
            {
                Id = g.Id,
                UserId = g.UserId,
                Title = g.Title,
                Symbol = g.Symbol,
                Description = g.Description,
                HardwareRequirements = g.HardwareRequirements,
                OtherInformation = g.OtherInformation,
                EngineId = g.EngineId,
                TypeId = g.TypeId,
                GenreSymbol = g.Genre != null ? g.Genre.Symbol : null,
                GenreName = g.Genre != null ? g.Genre.Name : null,
                EngineSymbol = g.Engine != null ? g.Engine.Symbol : null,
                EngineName = g.Engine != null ? g.Engine.Name : null,
                CoverUrl = g.CoverUrl,
                IntroUrl = g.IntroUrl,
                ScreenshotUrls = g.Screenshots
                    .OrderBy(s => s.SortOrder)
                    .Select(s => s.Url)
                    .ToList(),
                Screenshots = g.Screenshots
                    .OrderBy(s => s.SortOrder)
                    .Select(s => new GameScreenshotDto { Id = s.Id, Url = s.Url })
                    .ToList(),
                DraftP = g.DraftP,
                FeaturedP = g.FeaturedP,
                FeaturedOrder = g.FeaturedOrder,
                Kind = g.Kind,
                ParentGameId = g.ParentGameId,
                ParentSymbol = g.ParentGame != null ? g.ParentGame.Symbol : null,
                CreatedAtUtc = g.CreatedAtUtc,
                PackageSizeBytes = g.Builds
                    .OrderByDescending(b => b.IsDefault)
                    .Select(b => b.PackageSizeBytes)
                    .FirstOrDefault(),
                Builds = g.Builds
                    .OrderByDescending(b => b.IsDefault)
                    .ThenBy(b => b.Platform)
                    .Select(b => new GameBuildDto
                    {
                        Id = b.Id,
                        GameId = b.GameId,
                        Platform = b.Platform,
                        Architecture = b.Architecture,
                        CurrentVersion = b.CurrentVersion,
                        ExeName = b.ExeName,
                        ZipLink = b.ZipLink,
                        IsDefault = b.IsDefault
                    })
                    .ToList(),
                Dlcs = new List<GameDlcDto>()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private enum MediaSlot
    {
        Cover,
        Intro
    }
}
