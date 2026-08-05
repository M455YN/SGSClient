using System.Text.RegularExpressions;
using SGSClient.Core.Enums;

namespace SGSClient.Core.Packaging;

/// <summary>
/// Shared limits and rules for client preflight and server enforcement.
/// </summary>
public static partial class SgsPackageRules
{
    public const long MaxZipBytes = 2L * 1024 * 1024 * 1024;          // 2 GB compressed
    public const long MaxUncompressedBytes = 4L * 1024 * 1024 * 1024; // 4 GB unpacked
    public const int MaxEntryCount = 20_000;
    public const int MaxPathDepth = 12;
    public const int MaxEntryNameLength = 260;

    public const int MinTitleLength = 2;
    public const int MaxTitleLength = 128;
    public const int MinSymbolLength = 2;
    public const int MaxSymbolLength = 64;
    public const int MaxDescriptionLength = 4000;
    public const int MaxVersionLength = 32;
    public const int MaxExecutablePathLength = 260;
    public const int MaxScreenshots = 12;
    public const long MaxMediaFileBytes = 80L * 1024 * 1024; // 80 MB per media file

    public static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

    public static readonly HashSet<string> AllowedVideoExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm" };

    /// <summary>Compression ratio guard against zip bombs.</summary>
    public const double MaxCompressionRatio = 100.0;

    public static readonly string[] DefaultExcludeGlobs =
    [
        "**/*.pdb",
        "**/*.obj",
        "**/*.ilk",
        "**/*.exp",
        "**/*.iobj",
        "**/*.ipdb",
        "**/obj/**",
        "**/bin/Debug/**",
        "**/bin/Release/**",
        "**/.git/**",
        "**/.vs/**",
        "**/node_modules/**",
        "**/__MACOSX/**",
        "**/.DS_Store"
    ];

    public static bool IsValidSymbol(string? symbol) =>
        !string.IsNullOrWhiteSpace(symbol)
        && symbol.Length is >= MinSymbolLength and <= MaxSymbolLength
        && SymbolRegex().IsMatch(symbol);

    public static bool IsValidVersion(string? version) =>
        !string.IsNullOrWhiteSpace(version)
        && version.Length <= MaxVersionLength
        && VersionRegex().IsMatch(version);

    public static bool TryParsePlatform(string? value, out GamePlatform platform)
    {
        platform = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "windows":
            case "win":
            case "win64":
                platform = GamePlatform.Windows;
                return true;
            case "linux":
                platform = GamePlatform.Linux;
                return true;
            case "macos":
            case "osx":
            case "mac":
                platform = GamePlatform.MacOS;
                return true;
            default:
                return false;
        }
    }

    public static string ToManifestPlatform(GamePlatform platform) => platform switch
    {
        GamePlatform.Windows => "windows",
        GamePlatform.Linux => "linux",
        GamePlatform.MacOS => "macos",
        _ => "windows"
    };

    /// <summary>
    /// Reject absolute paths, drive letters, backslashes-only weirdness, and <c>..</c> segments.
    /// Normalized form uses forward slashes without a leading slash.
    /// </summary>
    public static bool TryNormalizeEntryPath(string? path, out string normalized, out string? error)
    {
        normalized = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Ścieżka jest pusta.";
            return false;
        }

        var raw = path.Trim().Replace('\\', '/');
        if (raw.StartsWith('/') || raw.Contains(':') || raw.Contains('\0'))
        {
            error = "Ścieżka nie może być absolutna ani zawierać niedozwolonych znaków.";
            return false;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = "Ścieżka jest pusta.";
            return false;
        }

        if (parts.Length > MaxPathDepth)
        {
            error = $"Zbyt głęboka ścieżka (max {MaxPathDepth} poziomów).";
            return false;
        }

        foreach (var part in parts)
        {
            if (part is "." or "..")
            {
                error = "Ścieżka nie może zawierać '.' ani '..'.";
                return false;
            }
        }

        normalized = string.Join('/', parts);
        if (normalized.Length > MaxEntryNameLength)
        {
            error = $"Ścieżka jest zbyt długa (max {MaxEntryNameLength}).";
            return false;
        }

        return true;
    }

    public static bool ExecutableMatchesPlatform(string executable, GamePlatform platform)
    {
        var name = Path.GetFileName(executable.Replace('\\', '/'));
        return platform switch
        {
            GamePlatform.Windows => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                    || name.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                                    || name.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase),
            GamePlatform.Linux => !name.Contains('.', StringComparison.Ordinal)
                                  || name.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)
                                  || name.EndsWith(".x86_64", StringComparison.OrdinalIgnoreCase),
            GamePlatform.MacOS => name.EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                                  || name.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)
                                  || !name.Contains('.', StringComparison.Ordinal),
            _ => false
        };
    }

    public static SgsPackageValidationResult ValidateManifest(SgsPackageManifest? manifest)
    {
        var errors = new List<string>();
        if (manifest is null)
        {
            return SgsPackageValidationResult.Fail($"Brak pliku {SgsPackageManifest.FileName}.");
        }

        if (manifest.SchemaVersion != SgsPackageManifest.CurrentSchemaVersion)
        {
            errors.Add($"Nieobsługiwana wersja schematu: {manifest.SchemaVersion} (oczekiwano {SgsPackageManifest.CurrentSchemaVersion}).");
        }

        var game = manifest.Game;
        if (game is null)
        {
            errors.Add("Sekcja 'game' jest wymagana.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(game.Title)
                || game.Title.Length < MinTitleLength
                || game.Title.Length > MaxTitleLength)
            {
                errors.Add($"Tytuł musi mieć {MinTitleLength}-{MaxTitleLength} znaków.");
            }

            if (!IsValidSymbol(game.Symbol))
            {
                errors.Add("Symbol musi być kebab-case (np. demo-runner), 2-64 znaki, małe litery.");
            }

            if (game.Description?.Length > MaxDescriptionLength)
            {
                errors.Add("Opis jest za długi.");
            }

            if (game.HardwareRequirements?.Length > 2000)
            {
                errors.Add("Wymagania sprzętowe są za długie.");
            }

            if (game.OtherInformation?.Length > 2000)
            {
                errors.Add("Pole 'otherInformation' jest za długie.");
            }

            var genre = SgsGameTaxonomy.NormalizeSlug(game.Genre);
            if (genre is not null)
            {
                if (!SgsGameTaxonomy.IsValidGenre(genre))
                {
                    errors.Add($"Nieznany gatunek '{game.Genre}'. Dozwolone: {string.Join(", ", SgsGameTaxonomy.Genres.Select(g => g.Symbol))}.");
                }
                else
                {
                    game.Genre = genre;
                }
            }

            var engine = SgsGameTaxonomy.NormalizeSlug(game.Engine);
            if (engine is not null)
            {
                if (!SgsGameTaxonomy.IsValidEngine(engine))
                {
                    errors.Add($"Nieznany silnik '{game.Engine}'. Dozwolone: {string.Join(", ", SgsGameTaxonomy.Engines.Select(e => e.Symbol))}.");
                }
                else
                {
                    game.Engine = engine;
                }
            }

            ValidateMedia(game.Media, errors);
        }

        var build = manifest.Build;
        if (build is null)
        {
            errors.Add("Sekcja 'build' jest wymagana.");
        }
        else
        {
            if (!IsValidVersion(build.Version))
            {
                errors.Add("Wersja musi być w formacie SemVer (np. 1.0.0).");
            }

            if (!TryParsePlatform(build.Platform, out var platform))
            {
                errors.Add("Platforma musi być: windows, linux lub macos.");
            }
            else if (!TryNormalizeEntryPath(build.Executable, out var exePath, out var pathError))
            {
                errors.Add($"executable: {pathError}");
            }
            else if (!ExecutableMatchesPlatform(exePath, platform))
            {
                errors.Add($"Plik uruchomieniowy nie pasuje do platformy {build.Platform}: {exePath}");
            }
            else
            {
                build.Executable = exePath;
            }

            if (build.Architecture is { Length: > 0 }
                && build.Architecture is not ("x64" or "x86" or "arm64"))
            {
                errors.Add("Architektura musi być: x64, x86 lub arm64.");
            }
        }

        return errors.Count == 0
            ? SgsPackageValidationResult.Ok()
            : SgsPackageValidationResult.Fail(errors.ToArray());
    }

    public static IEnumerable<string> EnumerateDeclaredMediaPaths(SgsPackageMediaInfo? media)
    {
        if (media is null)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(media.Cover))
        {
            yield return media.Cover;
        }

        if (!string.IsNullOrWhiteSpace(media.Intro))
        {
            yield return media.Intro;
        }

        if (media.Screenshots is null)
        {
            yield break;
        }

        foreach (var shot in media.Screenshots)
        {
            if (!string.IsNullOrWhiteSpace(shot))
            {
                yield return shot;
            }
        }
    }

    private static void ValidateMedia(SgsPackageMediaInfo? media, List<string> errors)
    {
        if (media is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(media.Cover))
        {
            if (!TryNormalizeMediaPath(media.Cover, AllowedImageExtensions, "cover", out var cover, out var coverError))
            {
                errors.Add(coverError!);
            }
            else
            {
                media.Cover = cover;
            }
        }
        else
        {
            media.Cover = null;
        }

        if (!string.IsNullOrWhiteSpace(media.Intro))
        {
            if (!TryNormalizeMediaPath(media.Intro, AllowedVideoExtensions, "intro", out var intro, out var introError))
            {
                errors.Add(introError!);
            }
            else
            {
                media.Intro = intro;
            }
        }
        else
        {
            media.Intro = null;
        }

        media.Screenshots ??= [];
        if (media.Screenshots.Count > MaxScreenshots)
        {
            errors.Add($"Maksymalnie {MaxScreenshots} screenów.");
        }

        var normalizedShots = new List<string>();
        for (var i = 0; i < media.Screenshots.Count; i++)
        {
            var raw = media.Screenshots[i];
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (!TryNormalizeMediaPath(raw, AllowedImageExtensions, $"screenshots[{i}]", out var shot, out var shotError))
            {
                errors.Add(shotError!);
                continue;
            }

            if (!normalizedShots.Contains(shot, StringComparer.OrdinalIgnoreCase))
            {
                normalizedShots.Add(shot);
            }
        }

        media.Screenshots = normalizedShots;
    }

    private static bool TryNormalizeMediaPath(
        string path,
        HashSet<string> allowedExtensions,
        string field,
        out string normalized,
        out string? error)
    {
        normalized = string.Empty;
        if (!TryNormalizeEntryPath(path, out normalized, out error))
        {
            error = $"{field}: {error}";
            return false;
        }

        var ext = Path.GetExtension(normalized);
        if (!allowedExtensions.Contains(ext))
        {
            error = $"{field}: niedozwolone rozszerzenie '{ext}'. Dozwolone: {string.Join(", ", allowedExtensions)}.";
            return false;
        }

        return true;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SymbolRegex();

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+(?:[-+][A-Za-z0-9.-]+)?$")]
    private static partial Regex VersionRegex();
}

public sealed class SgsPackageValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static SgsPackageValidationResult Ok() => new() { IsValid = true };

    public static SgsPackageValidationResult Fail(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };
}
