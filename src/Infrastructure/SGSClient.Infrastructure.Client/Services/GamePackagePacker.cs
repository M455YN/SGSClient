using SGSClient.Core.Localization;
using System.IO.Compression;
using System.Text.Json;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class GamePackagePacker : IGamePackagePacker
{
    public async Task<GamePackageManifestLoadResult> LoadManifestAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return GamePackageManifestLoadResult.Fail(Loc.T("Wybierz folder z grą."));
        }

        var manifestPath = Path.Combine(folderPath, SgsPackageManifest.FileName);
        if (!File.Exists(manifestPath))
        {
            return GamePackageManifestLoadResult.Fail(
                Loc.F("W folderze brakuje pliku {0}. Dodaj manifest i spróbuj ponownie.", SgsPackageManifest.FileName));
        }

        await using var stream = File.OpenRead(manifestPath);
        var (manifest, parseError) = await SgsPackageManifestSerializer.TryDeserializeAsync(stream, cancellationToken);
        if (parseError is not null || manifest is null)
        {
            return GamePackageManifestLoadResult.Fail(parseError ?? Loc.T("Nie udało się odczytać manifestu."));
        }

        return GamePackageManifestLoadResult.Ok(manifest);
    }

    public async Task<GamePackagePackResult> PackFolderAsync(
        string folderPath,
        SgsPackageManifest? manifestOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return GamePackagePackResult.Fail(Loc.T("Wybierz folder z grą."));
        }

        var manifestPath = Path.Combine(folderPath, SgsPackageManifest.FileName);
        SgsPackageManifest? manifest = manifestOverride;

        if (manifest is null)
        {
            if (!File.Exists(manifestPath))
            {
                return GamePackagePackResult.Fail(
                    Loc.F("W folderze brakuje pliku {0}. Dodaj manifest i spróbuj ponownie.", SgsPackageManifest.FileName));
            }

            await using var stream = File.OpenRead(manifestPath);
            var (loaded, parseError) = await SgsPackageManifestSerializer.TryDeserializeAsync(stream, cancellationToken);
            if (parseError is not null || loaded is null)
            {
                return GamePackagePackResult.Fail(parseError ?? Loc.T("Nie udało się odczytać manifestu."));
            }

            manifest = loaded;
        }
        else
        {
            // Preserve what the UI form does not edit (package hints, achievements, stats) from the file on disk;
            // otherwise rewriting the manifest below would silently drop them.
            if ((manifest.Package is null || manifest.Achievements is null) && File.Exists(manifestPath))
            {
                await using var stream = File.OpenRead(manifestPath);
                var (existing, _) = await SgsPackageManifestSerializer.TryDeserializeAsync(stream, cancellationToken);
                manifest.Package ??= existing?.Package;
                if (manifest.Achievements is null && existing?.Achievements is not null)
                {
                    manifest.Achievements = existing.Achievements;
                    manifest.Stats = existing.Stats;
                }
            }

            try
            {
                await using var write = File.Create(manifestPath);
                await JsonSerializer.SerializeAsync(write, manifest, SgsPackageManifestSerializer.Options, cancellationToken);
            }
            catch (Exception ex)
            {
                return GamePackagePackResult.Fail(Loc.F("Nie udało się zapisać manifestu: {0}", ex.Message));
            }
        }

        var validation = SgsPackageRules.ValidateManifest(manifest);
        if (!validation.IsValid)
        {
            return GamePackagePackResult.Fail(validation.Errors.ToArray());
        }

        SgsPackageRules.TryNormalizeEntryPath(manifest.Build.Executable, out var exeRelative, out _);
        var exeFull = Path.Combine(folderPath, exeRelative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(exeFull))
        {
            return GamePackagePackResult.Fail(Loc.F("Brak pliku uruchomieniowego w folderze: {0}", exeRelative));
        }

        foreach (var mediaPath in SgsPackageRules.EnumerateDeclaredMediaPaths(manifest.Game.Media))
        {
            if (!SgsPackageRules.TryNormalizeEntryPath(mediaPath, out var mediaRelative, out var mediaError))
            {
                return GamePackagePackResult.Fail($"media: {mediaError}");
            }

            var mediaFull = Path.Combine(folderPath, mediaRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(mediaFull))
            {
                return GamePackagePackResult.Fail(Loc.F("Brak pliku media w folderze: {0}", mediaRelative));
            }
        }

        var excludes = (manifest.Package?.Exclude?.Count > 0
                ? manifest.Package.Exclude
                : SgsPackageRules.DefaultExcludeGlobs)
            .Select(NormalizeGlob)
            .ToArray();

        var tempZip = Path.Combine(
            Path.GetTempPath(),
            $"sgs-pack-{manifest.Game.Symbol}-{manifest.Build.Version}-{Guid.NewGuid():N}.zip");

        try
        {
            if (File.Exists(tempZip))
            {
                File.Delete(tempZip);
            }

            var root = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    if (IsExcluded(relative, excludes))
                    {
                        continue;
                    }

                    if (!SgsPackageRules.TryNormalizeEntryPath(relative, out var normalized, out _))
                    {
                        continue;
                    }

                    archive.CreateEntryFromFile(file, normalized, CompressionLevel.Optimal);
                }
            }

            var info = new FileInfo(tempZip);
            if (info.Length == 0)
            {
                return GamePackagePackResult.Fail(Loc.T("Utworzona paczka jest pusta."));
            }

            if (info.Length > SgsPackageRules.MaxZipBytes)
            {
                return GamePackagePackResult.Fail(Loc.T("Paczka po spakowaniu jest za duża."));
            }

            return GamePackagePackResult.Ok(tempZip, manifest);
        }
        catch (Exception ex)
        {
            TryDelete(tempZip);
            return GamePackagePackResult.Fail(Loc.F("Pakowanie nie powiodło się: {0}", ex.Message));
        }
    }

    private static string NormalizeGlob(string glob) =>
        glob.Replace('\\', '/').Trim().TrimStart('/');

    private static bool IsExcluded(string relativePath, IReadOnlyList<string> globs)
    {
        foreach (var glob in globs)
        {
            if (MatchesSimpleGlob(relativePath, glob))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Minimal glob: supports **, *, and exact suffix/prefix patterns used in our defaults.</summary>
    private static bool MatchesSimpleGlob(string path, string glob)
    {
        path = path.Replace('\\', '/');
        glob = NormalizeGlob(glob);

        if (glob.StartsWith("**/", StringComparison.Ordinal))
        {
            var tail = glob[3..];
            if (tail.StartsWith("*.", StringComparison.Ordinal))
            {
                return path.EndsWith(tail[1..], StringComparison.OrdinalIgnoreCase);
            }

            if (tail.EndsWith("/**", StringComparison.Ordinal))
            {
                var folder = tail[..^3];
                return path.Equals(folder, StringComparison.OrdinalIgnoreCase)
                       || path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);
            }

            return path.EndsWith(tail, StringComparison.OrdinalIgnoreCase)
                   || path.Contains("/" + tail, StringComparison.OrdinalIgnoreCase);
        }

        if (glob.EndsWith("/**", StringComparison.Ordinal))
        {
            var folder = glob[..^3];
            return path.Equals(folder, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);
        }

        return path.Equals(glob, StringComparison.OrdinalIgnoreCase);
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
            // ignore
        }
    }
}
