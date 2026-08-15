using System.IO.Compression;
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

public sealed class GamePackagePublisher(
    SgsServerDbContext db,
    GameBuildVersionService versionService,
    IOptions<GameStorageSettings> storageOptions) : IGamePackagePublisher
{
    public async Task<GamePackagePublishResult> PublishZipAsync(
        Stream zipStream,
        long contentLength,
        int ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (contentLength <= 0)
        {
            return GamePackagePublishResult.Fail("Paczka jest pusta.");
        }

        if (contentLength > SgsPackageRules.MaxZipBytes)
        {
            return GamePackagePublishResult.Fail(
                $"Paczka jest za duża (max {SgsPackageRules.MaxZipBytes / (1024 * 1024)} MB).");
        }

        var storage = storageOptions.Value;
        var stagingDir = Path.Combine(storage.StoragePath, ".tmp");
        Directory.CreateDirectory(stagingDir);
        var tempZip = Path.Combine(stagingDir, $"sgs-upload-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var fs = File.Create(tempZip))
            {
                await zipStream.CopyToAsync(fs, cancellationToken);
            }

            var inspect = InspectZip(tempZip);
            if (!inspect.Success || inspect.Manifest is null)
            {
                return GamePackagePublishResult.Fail(inspect.Errors);
            }

            var manifest = inspect.Manifest;
            var manifestCheck = SgsPackageRules.ValidateManifest(manifest);
            if (!manifestCheck.IsValid)
            {
                return GamePackagePublishResult.Fail(manifestCheck.Errors);
            }

            SgsPackageRules.TryParsePlatform(manifest.Build.Platform, out var platform);
            SgsPackageRules.TryNormalizeEntryPath(manifest.Build.Executable, out var exePath, out _);

            Directory.CreateDirectory(storage.StoragePath);

            var fileName = $"{SgsPackageRules.ToManifestPlatform(platform)}-{(manifest.Build.Architecture ?? "x64")}.zip";
            var relativeDir = Path.Combine(manifest.Game.Symbol, manifest.Build.Version);
            var absoluteDir = Path.Combine(storage.StoragePath, relativeDir);
            Directory.CreateDirectory(absoluteDir);
            var absoluteZip = Path.Combine(absoluteDir, fileName);

            File.Copy(tempZip, absoluteZip, overwrite: true);

            var publicBase = storage.PublicBaseUrl.TrimEnd('/');
            var zipLink = $"{publicBase}/{manifest.Game.Symbol}/{manifest.Build.Version}/{fileName}"
                .Replace('\\', '/');

            var genreId = await ResolveGenreIdAsync(manifest.Game.Genre, cancellationToken);
            var engineId = await ResolveEngineIdAsync(manifest.Game.Engine, cancellationToken);

            var mediaResult = ExtractMedia(tempZip, manifest, storage);
            if (!mediaResult.Success)
            {
                return GamePackagePublishResult.Fail(mediaResult.Errors);
            }

            var isDlc = string.Equals(
                SgsPackageRules.NormalizeContentKind(manifest.Game.Kind),
                "dlc",
                StringComparison.Ordinal);
            int? parentGameId = null;
            if (isDlc)
            {
                var parent = await db.Games.AsNoTracking()
                    .FirstOrDefaultAsync(
                        g => g.Symbol == manifest.Game.ParentSymbol
                             && g.Kind == GameContentKind.Game,
                        cancellationToken);
                if (parent is null)
                {
                    return GamePackagePublishResult.Fail(
                        $"Nie znaleziono gry bazowej o symbolu '{manifest.Game.ParentSymbol}'.");
                }

                parentGameId = parent.Id;
            }

            var packageSize = new FileInfo(absoluteZip).Length;

            var game = await db.Games
                .Include(g => g.Builds)
                .Include(g => g.Screenshots)
                .FirstOrDefaultAsync(g => g.Symbol == manifest.Game.Symbol, cancellationToken);

            if (game is null)
            {
                game = new GameEntity
                {
                    UserId = ownerUserId,
                    Title = manifest.Game.Title.Trim(),
                    Symbol = manifest.Game.Symbol.Trim(),
                    Description = manifest.Game.Description,
                    HardwareRequirements = manifest.Game.HardwareRequirements,
                    OtherInformation = manifest.Game.OtherInformation,
                    TypeId = genreId,
                    EngineId = engineId,
                    CoverUrl = mediaResult.CoverUrl,
                    IntroUrl = mediaResult.IntroUrl,
                    DraftP = false,
                    FeaturedP = false,
                    Kind = isDlc ? GameContentKind.Dlc : GameContentKind.Game,
                    ParentGameId = parentGameId,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                db.Games.Add(game);
            }
            else
            {
                if (game.UserId != ownerUserId && ownerUserId > 0)
                {
                    return GamePackagePublishResult.Fail(
                        "Ta gra należy do innego autora. Nie możesz nadpisać cudzej paczki.");
                }

                var existingKind = isDlc ? GameContentKind.Dlc : GameContentKind.Game;
                if (game.Kind != existingKind)
                {
                    return GamePackagePublishResult.Fail(
                        "Nie można zmienić rodzaju pozycji (gra ↔ DLC) przy republishu.");
                }

                if (isDlc && game.ParentGameId != parentGameId)
                {
                    return GamePackagePublishResult.Fail(
                        "Nie można zmienić gry bazowej DLC przy republishu.");
                }

                game.Title = manifest.Game.Title.Trim();
                game.Description = manifest.Game.Description;
                game.HardwareRequirements = manifest.Game.HardwareRequirements;
                game.OtherInformation = manifest.Game.OtherInformation;
                game.TypeId = genreId;
                game.EngineId = engineId;
                game.CoverUrl = mediaResult.CoverUrl;
                game.IntroUrl = mediaResult.IntroUrl;

                if (game.Screenshots.Count > 0)
                {
                    db.GameScreenshots.RemoveRange(game.Screenshots);
                    game.Screenshots.Clear();
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            for (var i = 0; i < mediaResult.Screenshots.Count; i++)
            {
                var shot = mediaResult.Screenshots[i];
                db.GameScreenshots.Add(new GameScreenshotEntity
                {
                    GameId = game.Id,
                    SortOrder = i,
                    Url = shot.Url,
                    SourcePath = shot.SourcePath
                });
            }

            var arch = string.IsNullOrWhiteSpace(manifest.Build.Architecture)
                ? "x64"
                : manifest.Build.Architecture;
            var build = game.Builds.FirstOrDefault(b =>
                b.Platform == platform
                && string.Equals(b.Architecture ?? "x64", arch, StringComparison.OrdinalIgnoreCase));

            if (build is null)
            {
                build = new GameBuildEntity
                {
                    GameId = game.Id,
                    Platform = platform,
                    Architecture = arch,
                    CurrentVersion = manifest.Build.Version,
                    ExeName = exePath,
                    ZipLink = zipLink,
                    PackageSizeBytes = packageSize,
                    IsDefault = manifest.Build.IsDefault
                };
                db.GameBuilds.Add(build);
            }
            else
            {
                build.CurrentVersion = manifest.Build.Version;
                build.ExeName = exePath;
                build.ZipLink = zipLink;
                build.PackageSizeBytes = packageSize;
                build.IsDefault = manifest.Build.IsDefault;
            }

            if (manifest.Build.IsDefault)
            {
                foreach (var other in game.Builds.Where(b => b.Platform == platform && !ReferenceEquals(b, build)))
                {
                    other.IsDefault = false;
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            await versionService.RecordPublishedVersionAsync(
                build,
                manifest.Game.Symbol,
                manifest.Build.Version,
                exePath,
                zipLink,
                fileName,
                cancellationToken);

            var dto = await db.Games.AsNoTracking()
                .Where(g => g.Id == game.Id)
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
                    DraftP = g.DraftP,
                    FeaturedP = g.FeaturedP,
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
                        .ToList()
                })
                .FirstAsync(cancellationToken);

            return GamePackagePublishResult.Ok(dto);
        }
        finally
        {
            TryDelete(tempZip);
        }
    }

    private async Task<int?> ResolveGenreIdAsync(string? genreSymbol, CancellationToken cancellationToken)
    {
        var symbol = SgsGameTaxonomy.NormalizeSlug(genreSymbol);
        if (symbol is null)
        {
            return null;
        }

        return await db.GameGenres
            .Where(g => g.Symbol == symbol)
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<int?> ResolveEngineIdAsync(string? engineSymbol, CancellationToken cancellationToken)
    {
        var symbol = SgsGameTaxonomy.NormalizeSlug(engineSymbol);
        if (symbol is null)
        {
            return null;
        }

        return await db.GameEngines
            .Where(e => e.Symbol == symbol)
            .Select(e => (int?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static MediaExtractResult ExtractMedia(
        string zipPath,
        SgsPackageManifest manifest,
        GameStorageSettings storage)
    {
        var media = manifest.Game.Media;
        if (media is null
            || (string.IsNullOrWhiteSpace(media.Cover)
                && string.IsNullOrWhiteSpace(media.Intro)
                && (media.Screenshots is null || media.Screenshots.Count == 0)))
        {
            return MediaExtractResult.Empty();
        }

        var mediaRoot = Path.Combine(storage.StoragePath, manifest.Game.Symbol, "media");
        Directory.CreateDirectory(mediaRoot);

        var mediaBase = storage.MediaPublicBaseUrl.TrimEnd('/');
        string? coverUrl = null;
        string? introUrl = null;
        var screenshots = new List<(string Url, string SourcePath)>();

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);

            if (!string.IsNullOrWhiteSpace(media.Cover))
            {
                var extracted = ExtractOne(archive, media.Cover, mediaRoot, mediaBase, manifest.Game.Symbol);
                if (!extracted.Success)
                {
                    return MediaExtractResult.Fail(extracted.Error!);
                }

                coverUrl = extracted.Url;
            }

            if (!string.IsNullOrWhiteSpace(media.Intro))
            {
                var extracted = ExtractOne(archive, media.Intro, mediaRoot, mediaBase, manifest.Game.Symbol);
                if (!extracted.Success)
                {
                    return MediaExtractResult.Fail(extracted.Error!);
                }

                introUrl = extracted.Url;
            }

            if (media.Screenshots is not null)
            {
                foreach (var shot in media.Screenshots)
                {
                    var extracted = ExtractOne(archive, shot, mediaRoot, mediaBase, manifest.Game.Symbol);
                    if (!extracted.Success)
                    {
                        return MediaExtractResult.Fail(extracted.Error!);
                    }

                    screenshots.Add((extracted.Url!, shot));
                }
            }

            return MediaExtractResult.Ok(coverUrl, introUrl, screenshots);
        }
        catch (Exception ex)
        {
            return MediaExtractResult.Fail($"Nie udało się wyodrębnić mediów: {ex.Message}");
        }
    }

    private static (bool Success, string? Url, string? Error) ExtractOne(
        ZipArchive archive,
        string relativePath,
        string mediaRoot,
        string mediaBase,
        string symbol)
    {
        if (!SgsPackageRules.TryNormalizeEntryPath(relativePath, out var normalized, out var pathError))
        {
            return (false, null, $"media: {pathError}");
        }

        var entry = archive.Entries.FirstOrDefault(e =>
            SgsPackageRules.TryNormalizeEntryPath(e.FullName.TrimEnd('/'), out var name, out _)
            && string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return (false, null, $"W paczce brak pliku media: {normalized}");
        }

        if (entry.Length > SgsPackageRules.MaxMediaFileBytes)
        {
            return (false, null, $"Plik media jest za duży: {normalized}");
        }

        var safeName = normalized.Replace('/', '_');
        var dest = Path.Combine(mediaRoot, safeName);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        entry.ExtractToFile(dest, overwrite: true);

        var url = $"{mediaBase}/{symbol}/{safeName}".Replace('\\', '/');
        return (true, url, null);
    }

    private static ZipInspectResult InspectZip(string zipPath)
    {
        var errors = new List<string>();
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count == 0)
            {
                return ZipInspectResult.Fail("Archiwum ZIP jest puste.");
            }

            if (archive.Entries.Count > SgsPackageRules.MaxEntryCount)
            {
                return ZipInspectResult.Fail($"Za dużo plików w paczce (max {SgsPackageRules.MaxEntryCount}).");
            }

            long uncompressed = 0;
            string? manifestJson = null;
            var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
                {
                    continue;
                }

                if (!SgsPackageRules.TryNormalizeEntryPath(entry.FullName.TrimEnd('/'), out var normalized, out var pathError))
                {
                    errors.Add($"{entry.FullName}: {pathError}");
                    continue;
                }

                entryNames.Add(normalized);
                uncompressed += entry.Length;
                if (uncompressed > SgsPackageRules.MaxUncompressedBytes)
                {
                    return ZipInspectResult.Fail("Rozpakowana paczka przekracza dozwolony rozmiar.");
                }

                var compressed = new FileInfo(zipPath).Length;
                if (compressed > 0 && uncompressed / (double)compressed > SgsPackageRules.MaxCompressionRatio)
                {
                    return ZipInspectResult.Fail("Podejrzany współczynnik kompresji (ochrona przed zip bomb).");
                }

                if (string.Equals(normalized, SgsPackageManifest.FileName, StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith("/" + SgsPackageManifest.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    manifestJson = reader.ReadToEnd();
                }
            }

            if (errors.Count > 0)
            {
                return ZipInspectResult.Fail(errors.Take(10).ToArray());
            }

            if (manifestJson is null)
            {
                return ZipInspectResult.Fail(
                    $"Brak pliku {SgsPackageManifest.FileName} w katalogu głównym paczki.");
            }

            var (manifest, parseError) = SgsPackageManifestSerializer.TryDeserialize(manifestJson);
            if (parseError is not null || manifest is null)
            {
                return ZipInspectResult.Fail(parseError ?? "Niepoprawny JSON w manifeście.");
            }

            if (manifest.Build?.Executable is null)
            {
                return ZipInspectResult.Fail("Manifest nie zawiera ścieżki executable.");
            }

            if (!SgsPackageRules.TryNormalizeEntryPath(manifest.Build.Executable, out var exe, out var exeError))
            {
                return ZipInspectResult.Fail($"executable: {exeError}");
            }

            if (!entryNames.Contains(exe))
            {
                return ZipInspectResult.Fail($"W paczce brak pliku uruchomieniowego: {exe}");
            }

            manifest.Build.Executable = exe;

            foreach (var mediaPath in SgsPackageRules.EnumerateDeclaredMediaPaths(manifest.Game.Media))
            {
                if (!SgsPackageRules.TryNormalizeEntryPath(mediaPath, out var mediaNorm, out var mediaError))
                {
                    return ZipInspectResult.Fail($"media: {mediaError}");
                }

                if (!entryNames.Contains(mediaNorm))
                {
                    return ZipInspectResult.Fail($"W paczce brak pliku media: {mediaNorm}");
                }
            }

            return ZipInspectResult.Ok(manifest);
        }
        catch (InvalidDataException)
        {
            return ZipInspectResult.Fail("Plik nie jest prawidłowym archiwum ZIP.");
        }
        catch (Exception ex)
        {
            return ZipInspectResult.Fail($"Nie udało się odczytać paczki: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
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
            // ignore temp cleanup failures
        }
    }

    private sealed class ZipInspectResult
    {
        public bool Success { get; init; }
        public SgsPackageManifest? Manifest { get; init; }
        public IReadOnlyList<string> Errors { get; init; } = [];

        public static ZipInspectResult Ok(SgsPackageManifest manifest) => new()
        {
            Success = true,
            Manifest = manifest
        };

        public static ZipInspectResult Fail(params string[] errors) => new()
        {
            Success = false,
            Errors = errors
        };
    }

    private sealed class MediaExtractResult
    {
        public bool Success { get; init; }
        public string? CoverUrl { get; init; }
        public string? IntroUrl { get; init; }
        public IReadOnlyList<(string Url, string SourcePath)> Screenshots { get; init; } = [];
        public IReadOnlyList<string> Errors { get; init; } = [];

        public static MediaExtractResult Empty() => new() { Success = true };

        public static MediaExtractResult Ok(
            string? coverUrl,
            string? introUrl,
            IReadOnlyList<(string Url, string SourcePath)> screenshots) => new()
        {
            Success = true,
            CoverUrl = coverUrl,
            IntroUrl = introUrl,
            Screenshots = screenshots
        };

        public static MediaExtractResult Fail(params string[] errors) => new()
        {
            Success = false,
            Errors = errors
        };
    }
}
