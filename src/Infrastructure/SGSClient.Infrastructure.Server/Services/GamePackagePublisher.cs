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
        bool allowOverwriteForeignGame = false,
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

            var genres = await ResolveTaxonomyAsync(db.GameGenres, manifest.Game.Genres, g => g.Symbol, cancellationToken);
            var engines = await ResolveTaxonomyAsync(db.GameEngines, manifest.Game.Engines, e => e.Symbol, cancellationToken);

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
                .Include(g => g.Genres)
                .Include(g => g.Engines)
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
                    Genres = genres,
                    Engines = engines,
                    CoverUrl = mediaResult.CoverUrl,
                    HeaderUrl = mediaResult.HeaderUrl,
                    LogoUrl = mediaResult.LogoUrl,
                    IntroUrl = mediaResult.IntroUrl,
                    DraftP = false,
                    FeaturedP = false,
                    Kind = isDlc ? GameContentKind.Dlc : GameContentKind.Game,
                    ParentGameId = parentGameId,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                };
                ApplyStoreMetadata(game, manifest.Game, overwriteMissing: true);
                db.Games.Add(game);
            }
            else
            {
                if (!allowOverwriteForeignGame && game.UserId != ownerUserId && ownerUserId > 0)
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
                game.Genres.Clear();
                foreach (var genre in genres)
                {
                    game.Genres.Add(genre);
                }

                game.Engines.Clear();
                foreach (var engine in engines)
                {
                    game.Engines.Add(engine);
                }

                ApplyStoreMetadata(game, manifest.Game, overwriteMissing: false);
                if (mediaResult.CoverUrl is not null)
                {
                    game.CoverUrl = mediaResult.CoverUrl;
                }

                if (mediaResult.HeaderUrl is not null)
                {
                    game.HeaderUrl = mediaResult.HeaderUrl;
                }

                if (mediaResult.LogoUrl is not null)
                {
                    game.LogoUrl = mediaResult.LogoUrl;
                }

                if (mediaResult.IntroUrl is not null)
                {
                    game.IntroUrl = mediaResult.IntroUrl;
                }

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
                manifest.Build.ReleaseNotes,
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
                    Genres = g.Genres.OrderBy(x => x.Id).Select(x => new SgsGameTaxonomy.TaxonomyItem(x.Symbol, x.Name)).ToList(),
                    Engines = g.Engines.OrderBy(x => x.Id).Select(x => new SgsGameTaxonomy.TaxonomyItem(x.Symbol, x.Name)).ToList(),
                    CoverUrl = g.CoverUrl,
                    HeaderUrl = g.HeaderUrl,
                    LogoUrl = g.LogoUrl,
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
                    StudioName = g.StudioName,
                    WebsiteUrl = g.WebsiteUrl,
                    DiscordUrl = g.DiscordUrl,
                    SupportUrl = g.SupportUrl,
                    ReleaseStatus = g.ReleaseStatus,
                    ReleaseDateUtc = g.ReleaseDateUtc,
                    AgeRating = g.AgeRating,
                    Features = g.Features,
                    LanguagesCsv = g.LanguagesCsv,
                    TagsCsv = g.TagsCsv,
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

    private static void ApplyStoreMetadata(GameEntity game, SgsPackageGameInfo info, bool overwriteMissing)
    {
        if (!string.IsNullOrWhiteSpace(info.StudioName) || overwriteMissing)
        {
            game.StudioName = string.IsNullOrWhiteSpace(info.StudioName) ? null : info.StudioName.Trim();
        }

        if (info.WebsiteUrl is not null || overwriteMissing)
        {
            game.WebsiteUrl = info.WebsiteUrl;
        }

        if (info.DiscordUrl is not null || overwriteMissing)
        {
            game.DiscordUrl = info.DiscordUrl;
        }

        if (info.SupportUrl is not null || overwriteMissing)
        {
            game.SupportUrl = info.SupportUrl;
        }

        if (!string.IsNullOrWhiteSpace(info.ReleaseStatus)
            && SgsPackageRules.TryParseReleaseStatus(info.ReleaseStatus, out var status))
        {
            game.ReleaseStatus = status;
        }
        else if (overwriteMissing)
        {
            game.ReleaseStatus = GameReleaseStatus.Released;
        }

        if (!string.IsNullOrWhiteSpace(info.ReleaseDate)
            && SgsPackageRules.TryParseReleaseDate(info.ReleaseDate, out var date, out _))
        {
            game.ReleaseDateUtc = date;
        }
        else if (overwriteMissing)
        {
            game.ReleaseDateUtc = null;
        }

        if (!string.IsNullOrWhiteSpace(info.AgeRating)
            && SgsPackageRules.TryParseAgeRating(info.AgeRating, out var age))
        {
            game.AgeRating = age;
        }
        else if (overwriteMissing)
        {
            game.AgeRating = GameAgeRating.Unrated;
        }

        if (info.Features is not null
            && SgsPackageRules.TryParseFeatures(info.Features, out var features, out _))
        {
            game.Features = features;
        }
        else if (overwriteMissing)
        {
            game.Features = GameFeatures.None;
        }

        if (info.Languages is not null)
        {
            game.LanguagesCsv = CsvList.Join(info.Languages);
        }
        else if (overwriteMissing)
        {
            game.LanguagesCsv = null;
        }

        if (info.Tags is not null)
        {
            game.TagsCsv = CsvList.Join(info.Tags);
        }
        else if (overwriteMissing)
        {
            game.TagsCsv = null;
        }
    }

    private static async Task<List<T>> ResolveTaxonomyAsync<T>(
        IQueryable<T> source,
        IList<string>? slugs,
        Func<T, string> symbolOf,
        CancellationToken cancellationToken)
    {
        var wanted = SgsGameTaxonomy.MergeSlugs(null, slugs);
        if (wanted.Count == 0)
        {
            return [];
        }

        var all = await source.ToListAsync(cancellationToken);
        return all.Where(x => wanted.Contains(symbolOf(x))).ToList();
    }

    private static MediaExtractResult ExtractMedia(
        string zipPath,
        SgsPackageManifest manifest,
        GameStorageSettings storage)
    {
        var media = manifest.Game.Media;
        if (media is null
            || (string.IsNullOrWhiteSpace(media.Cover)
                && string.IsNullOrWhiteSpace(media.Header)
                && string.IsNullOrWhiteSpace(media.Logo)
                && string.IsNullOrWhiteSpace(media.Intro)
                && (media.Screenshots is null || media.Screenshots.Count == 0)))
        {
            return MediaExtractResult.Empty();
        }

        var mediaRoot = Path.Combine(storage.StoragePath, manifest.Game.Symbol, "media");
        Directory.CreateDirectory(mediaRoot);

        var mediaBase = storage.MediaPublicBaseUrl.TrimEnd('/');
        string? coverUrl = null;
        string? headerUrl = null;
        string? logoUrl = null;
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

            if (!string.IsNullOrWhiteSpace(media.Header))
            {
                var extracted = ExtractOne(archive, media.Header, mediaRoot, mediaBase, manifest.Game.Symbol);
                if (!extracted.Success)
                {
                    return MediaExtractResult.Fail(extracted.Error!);
                }

                headerUrl = extracted.Url;
            }

            if (!string.IsNullOrWhiteSpace(media.Logo))
            {
                var extracted = ExtractOne(archive, media.Logo, mediaRoot, mediaBase, manifest.Game.Symbol);
                if (!extracted.Success)
                {
                    return MediaExtractResult.Fail(extracted.Error!);
                }

                logoUrl = extracted.Url;
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

            return MediaExtractResult.Ok(coverUrl, headerUrl, logoUrl, introUrl, screenshots);
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
        public string? HeaderUrl { get; init; }
        public string? LogoUrl { get; init; }
        public string? IntroUrl { get; init; }
        public IReadOnlyList<(string Url, string SourcePath)> Screenshots { get; init; } = [];
        public IReadOnlyList<string> Errors { get; init; } = [];

        public static MediaExtractResult Empty() => new() { Success = true };

        public static MediaExtractResult Ok(
            string? coverUrl,
            string? headerUrl,
            string? logoUrl,
            string? introUrl,
            IReadOnlyList<(string Url, string SourcePath)> screenshots) => new()
        {
            Success = true,
            CoverUrl = coverUrl,
            HeaderUrl = headerUrl,
            LogoUrl = logoUrl,
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
