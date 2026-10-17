using SGSClient.Core.Achievements;
using SGSClient.Core.Localization;
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
    public const int MaxReleaseNotesLength = 4000;
    public const int MaxVersionLength = 32;
    public const int MaxExecutablePathLength = 260;
    public const int MaxScreenshots = 12;
    public const long MaxMediaFileBytes = 80L * 1024 * 1024; // 80 MB per media file
    public const int MaxStudioNameLength = 128;
    public const int MaxUrlLength = 2048;
    public const int MaxFeatureCount = 16;
    public const int MaxLanguageCount = 32;
    public const int MaxTagCount = 32;
    public const int MaxCsvFieldLength = 512;

    public static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif" };

    public static readonly string[] FeatureSlugs =
    [
        "singleplayer",
        "multiplayer",
        "coop",
        "controller",
        "cloud",
        "achievements",
        "modding",
        "vr"
    ];

    public static readonly string[] ReleaseStatusSlugs =
    [
        "announced",
        "earlyAccess",
        "released",
        "demo"
    ];

    public static readonly string[] AgeRatingSlugs =
    [
        "unrated",
        "pegi3",
        "pegi7",
        "pegi12",
        "pegi16",
        "pegi18"
    ];

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

    /// <summary>
    /// Path-safe storage folder segment for serving packages/media.
    /// Broader than <see cref="IsValidSymbol"/> so legacy PascalCase / spaced symbols still work.
    /// </summary>
    public static bool IsSafeStorageSegment(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length is >= MinSymbolLength and <= MaxSymbolLength
        && !value.Contains("..", StringComparison.Ordinal)
        && !value.Contains('/')
        && !value.Contains('\\')
        && SafeStorageSegmentRegex().IsMatch(value);

    public static bool IsValidVersion(string? version) =>
        !string.IsNullOrWhiteSpace(version)
        && version.Length <= MaxVersionLength
        && VersionRegex().IsMatch(version);

    /// <summary>Returns "game", "dlc" or "app", or null if invalid. Null/empty kind defaults to game.</summary>
    public static string? NormalizeContentKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return "game";
        }

        return kind.Trim().ToLowerInvariant() switch
        {
            "game" => "game",
            "dlc" => "dlc",
            "app" => "app",
            _ => null
        };
    }

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
            error = Loc.T("Ścieżka jest pusta.");
            return false;
        }

        var raw = path.Trim().Replace('\\', '/');
        if (raw.StartsWith('/') || raw.Contains(':') || raw.Contains('\0'))
        {
            error = Loc.T("Ścieżka nie może być absolutna ani zawierać niedozwolonych znaków.");
            return false;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = Loc.T("Ścieżka jest pusta.");
            return false;
        }

        if (parts.Length > MaxPathDepth)
        {
            error = Loc.F("Zbyt głęboka ścieżka (max {0} poziomów).", MaxPathDepth);
            return false;
        }

        foreach (var part in parts)
        {
            if (part is "." or "..")
            {
                error = Loc.T("Ścieżka nie może zawierać '.' ani '..'.");
                return false;
            }
        }

        normalized = string.Join('/', parts);
        if (normalized.Length > MaxEntryNameLength)
        {
            error = Loc.F("Ścieżka jest zbyt długa (max {0}).", MaxEntryNameLength);
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
            return SgsPackageValidationResult.Fail(Loc.F("Brak pliku {0}.", SgsPackageManifest.FileName));
        }

        if (manifest.SchemaVersion != SgsPackageManifest.CurrentSchemaVersion)
        {
            errors.Add(Loc.F("Nieobsługiwana wersja schematu: {0} (oczekiwano {1}).", manifest.SchemaVersion, SgsPackageManifest.CurrentSchemaVersion));
        }

        var game = manifest.Game;
        if (game is null)
        {
            errors.Add(Loc.T("Sekcja 'game' jest wymagana."));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(game.Title)
                || game.Title.Length < MinTitleLength
                || game.Title.Length > MaxTitleLength)
            {
                errors.Add(Loc.F("Tytuł musi mieć {0}-{1} znaków.", MinTitleLength, MaxTitleLength));
            }

            if (!IsValidSymbol(game.Symbol))
            {
                errors.Add(Loc.T("Symbol musi być kebab-case (np. demo-runner), 2-64 znaki, małe litery."));
            }

            if (game.Description?.Length > MaxDescriptionLength)
            {
                errors.Add(Loc.T("Opis jest za długi."));
            }

            if (game.HardwareRequirements?.Length > 2000)
            {
                errors.Add(Loc.T("Wymagania sprzętowe są za długie."));
            }

            if (game.OtherInformation?.Length > 2000)
            {
                errors.Add(Loc.T("Pole 'otherInformation' jest za długie."));
            }

            var kind = NormalizeContentKind(game.Kind);
            if (kind is null)
            {
                errors.Add(Loc.T("Pole 'kind' musi być: game, dlc lub app."));
            }
            else
            {
                game.Kind = kind;
                if (kind == "dlc")
                {
                    if (!IsValidSymbol(game.ParentSymbol))
                    {
                        errors.Add("DLC wymaga parentSymbol (kebab-case symbol gry bazowej).");
                    }
                    else if (string.Equals(game.ParentSymbol, game.Symbol, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(Loc.T("parentSymbol nie może być taki sam jak symbol DLC."));
                    }
                    else
                    {
                        game.ParentSymbol = game.ParentSymbol!.Trim().ToLowerInvariant();
                    }
                }
                else
                {
                    game.ParentSymbol = null;
                }
            }

            var genres = SgsGameTaxonomy.MergeSlugs(game.Genre, game.Genres);
            foreach (var genre in genres.Where(g => !SgsGameTaxonomy.IsValidGenre(g)))
            {
                errors.Add(Loc.F("Nieznany gatunek '{0}'. Dozwolone: {1}.", genre, string.Join(", ", SgsGameTaxonomy.Genres.Select(g => g.Symbol))));
            }

            if (genres.Count > SgsGameTaxonomy.MaxGenres)
            {
                errors.Add(Loc.F("Maksymalnie {0} gatunków na grę.", SgsGameTaxonomy.MaxGenres));
            }

            var engines = SgsGameTaxonomy.MergeSlugs(game.Engine, game.Engines);
            foreach (var engine in engines.Where(e => !SgsGameTaxonomy.IsValidEngine(e)))
            {
                errors.Add(Loc.F("Nieznany silnik '{0}'. Dozwolone: {1}.", engine, string.Join(", ", SgsGameTaxonomy.Engines.Select(e => e.Symbol))));
            }

            if (engines.Count > SgsGameTaxonomy.MaxEngines)
            {
                errors.Add(Loc.F("Maksymalnie {0} silniki na grę.", SgsGameTaxonomy.MaxEngines));
            }

            game.Genres = genres;
            game.Genre = genres.FirstOrDefault();
            game.Engines = engines;
            game.Engine = engines.FirstOrDefault();

            ValidateStoreMetadata(game, errors);
            ValidateMedia(game.Media, errors);
        }

        var build = manifest.Build;
        if (build is null)
        {
            errors.Add(Loc.T("Sekcja 'build' jest wymagana."));
        }
        else
        {
            if (!IsValidVersion(build.Version))
            {
                errors.Add(Loc.T("Wersja musi być w formacie SemVer (np. 1.0.0)."));
            }

            if (!TryParsePlatform(build.Platform, out var platform))
            {
                errors.Add(Loc.T("Platforma musi być: windows, linux lub macos."));
            }
            else if (!TryNormalizeEntryPath(build.Executable, out var exePath, out var pathError))
            {
                errors.Add($"executable: {pathError}");
            }
            else if (!ExecutableMatchesPlatform(exePath, platform))
            {
                errors.Add(Loc.F("Plik uruchomieniowy nie pasuje do platformy {0}: {1}", build.Platform, exePath));
            }
            else
            {
                build.Executable = exePath;
            }

            if (build.ReleaseNotes?.Length > MaxReleaseNotesLength)
            {
                errors.Add(Loc.F("Opis zmian może mieć maksymalnie {0} znaków.", MaxReleaseNotesLength));
            }

            if (build.Architecture is { Length: > 0 }
                && build.Architecture is not ("x64" or "x86" or "arm64"))
            {
                errors.Add(Loc.T("Architektura musi być: x64, x86 lub arm64."));
            }
        }

        ValidateAchievements(manifest.Achievements, errors);

        return errors.Count == 0
            ? SgsPackageValidationResult.Ok()
            : SgsPackageValidationResult.Fail(errors.ToArray());
    }

    public static bool TryParseReleaseStatus(string? value, out GameReleaseStatus status)
    {
        status = GameReleaseStatus.Released;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal)
                    .ToLowerInvariant())
        {
            case "announced":
                status = GameReleaseStatus.Announced;
                return true;
            case "earlyaccess":
                status = GameReleaseStatus.EarlyAccess;
                return true;
            case "released":
                status = GameReleaseStatus.Released;
                return true;
            case "demo":
                status = GameReleaseStatus.Demo;
                return true;
            default:
                return false;
        }
    }

    public static string ToManifestReleaseStatus(GameReleaseStatus status) => status switch
    {
        GameReleaseStatus.Announced => "announced",
        GameReleaseStatus.EarlyAccess => "earlyAccess",
        GameReleaseStatus.Demo => "demo",
        _ => "released"
    };

    public static bool TryParseAgeRating(string? value, out GameAgeRating rating)
    {
        rating = GameAgeRating.Unrated;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var raw = value.Trim().ToLowerInvariant().Replace("-", "", StringComparison.Ordinal);
        if (raw is "unrated" or "0")
        {
            return true;
        }

        if (raw.StartsWith("pegi", StringComparison.Ordinal))
        {
            raw = raw["pegi".Length..];
        }

        if (!int.TryParse(raw, out var n) || !Enum.IsDefined(typeof(GameAgeRating), n))
        {
            return false;
        }

        rating = (GameAgeRating)n;
        return true;
    }

    public static string ToManifestAgeRating(GameAgeRating rating) => rating switch
    {
        GameAgeRating.Pegi3 => "pegi3",
        GameAgeRating.Pegi7 => "pegi7",
        GameAgeRating.Pegi12 => "pegi12",
        GameAgeRating.Pegi16 => "pegi16",
        GameAgeRating.Pegi18 => "pegi18",
        _ => "unrated"
    };

    public static bool TryParseReleaseDate(string? value, out DateTimeOffset date, out string? error)
    {
        date = default;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = Loc.T("Pusta data wydania.");
            return false;
        }

        if (DateTimeOffset.TryParse(
                value.Trim(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out date))
        {
            return true;
        }

        error = Loc.T("Data wydania musi być w formacie ISO (np. 2024-06-15).");
        return false;
    }

    public static bool TryParseFeatures(IEnumerable<string>? values, out GameFeatures features, out string? error)
    {
        features = GameFeatures.None;
        error = null;
        if (values is null)
        {
            return true;
        }

        var list = values
            .Select(v => v?.Trim() ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();
        if (list.Count > MaxFeatureCount)
        {
            error = $"Maksymalnie {MaxFeatureCount} funkcji.";
            return false;
        }

        foreach (var raw in list)
        {
            var slug = NormalizeFeatureSlug(raw);
            if (slug is null || !TryMapFeature(slug, out var flag))
            {
                error = Loc.F("Nieznana funkcja '{0}'. Dozwolone: {1}.", raw, string.Join(", ", FeatureSlugs));
                return false;
            }

            features |= flag;
        }

        return true;
    }

    public static IReadOnlyList<string> ToManifestFeatures(GameFeatures features)
    {
        var list = new List<string>(8);
        if (features.HasFlag(GameFeatures.SinglePlayer)) list.Add("singleplayer");
        if (features.HasFlag(GameFeatures.MultiPlayer)) list.Add("multiplayer");
        if (features.HasFlag(GameFeatures.CoOp)) list.Add("coop");
        if (features.HasFlag(GameFeatures.ControllerSupport)) list.Add("controller");
        if (features.HasFlag(GameFeatures.CloudSaves)) list.Add("cloud");
        if (features.HasFlag(GameFeatures.Achievements)) list.Add("achievements");
        if (features.HasFlag(GameFeatures.Modding)) list.Add("modding");
        if (features.HasFlag(GameFeatures.Vr)) list.Add("vr");
        return list;
    }

    public static bool TryNormalizeHttpUrl(string? value, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > MaxUrlLength)
        {
            error = Loc.T("Adres URL jest za długi.");
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            error = Loc.T("Adres URL musi zaczynać się od http:// lub https://.");
            return false;
        }

        normalized = trimmed;
        return true;
    }

    public static IList<string>? NormalizeStringList(IEnumerable<string>? values, int maxCount, bool lowerCase)
    {
        if (values is null)
        {
            return null;
        }

        var cleaned = values
            .Select(v => v?.Trim() ?? string.Empty)
            .Where(v => v.Length > 0)
            .Select(v => lowerCase ? v.ToLowerInvariant() : v)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();

        return cleaned;
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

        if (!string.IsNullOrWhiteSpace(media.Header))
        {
            yield return media.Header;
        }

        if (!string.IsNullOrWhiteSpace(media.Logo))
        {
            yield return media.Logo;
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

    private static void ValidateStoreMetadata(SgsPackageGameInfo game, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(game.StudioName))
        {
            var studio = game.StudioName.Trim();
            if (studio.Length > MaxStudioNameLength)
            {
                errors.Add(Loc.F("Nazwa studia może mieć maksymalnie {0} znaków.", MaxStudioNameLength));
            }
            else
            {
                game.StudioName = studio;
            }
        }
        else
        {
            game.StudioName = null;
        }

        if (!TryNormalizeHttpUrl(game.WebsiteUrl, out var website, out var websiteError))
        {
            errors.Add($"websiteUrl: {websiteError}");
        }
        else
        {
            game.WebsiteUrl = website;
        }

        if (!TryNormalizeHttpUrl(game.DiscordUrl, out var discord, out var discordError))
        {
            errors.Add($"discordUrl: {discordError}");
        }
        else
        {
            game.DiscordUrl = discord;
        }

        if (!TryNormalizeHttpUrl(game.SupportUrl, out var support, out var supportError))
        {
            errors.Add($"supportUrl: {supportError}");
        }
        else
        {
            game.SupportUrl = support;
        }

        if (!string.IsNullOrWhiteSpace(game.ReleaseStatus))
        {
            if (!TryParseReleaseStatus(game.ReleaseStatus, out var status))
            {
                errors.Add(Loc.F("Nieznany status wydania '{0}'. Dozwolone: {1}.", game.ReleaseStatus, string.Join(", ", ReleaseStatusSlugs)));
            }
            else
            {
                game.ReleaseStatus = ToManifestReleaseStatus(status);
            }
        }
        else
        {
            game.ReleaseStatus = null;
        }

        if (!string.IsNullOrWhiteSpace(game.ReleaseDate))
        {
            if (!TryParseReleaseDate(game.ReleaseDate, out var date, out var dateError))
            {
                errors.Add(dateError ?? Loc.T("Nieprawidłowa data wydania."));
            }
            else
            {
                game.ReleaseDate = date.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        else
        {
            game.ReleaseDate = null;
        }

        if (!string.IsNullOrWhiteSpace(game.AgeRating))
        {
            if (!TryParseAgeRating(game.AgeRating, out var rating))
            {
                errors.Add(Loc.F("Nieznana kategoria wiekowa '{0}'. Dozwolone: {1}.", game.AgeRating, string.Join(", ", AgeRatingSlugs)));
            }
            else
            {
                game.AgeRating = ToManifestAgeRating(rating);
            }
        }
        else
        {
            game.AgeRating = null;
        }

        if (game.Features is not null)
        {
            if (!TryParseFeatures(game.Features, out _, out var featureError))
            {
                errors.Add(featureError!);
            }
            else
            {
                game.Features = game.Features
                    .Select(NormalizeFeatureSlug)
                    .Where(s => s is not null)
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        if (game.Languages is not null)
        {
            if (game.Languages.Count > MaxLanguageCount)
            {
                errors.Add(Loc.F("Maksymalnie {0} języków.", MaxLanguageCount));
            }

            game.Languages = NormalizeStringList(game.Languages, MaxLanguageCount, lowerCase: false) ?? [];
            var joined = string.Join(",", game.Languages);
            if (joined.Length > MaxCsvFieldLength)
            {
                errors.Add(Loc.F("Lista języków jest za długa (max {0} znaków po złączeniu).", MaxCsvFieldLength));
            }
        }

        if (game.Tags is not null)
        {
            if (game.Tags.Count > MaxTagCount)
            {
                errors.Add(Loc.F("Maksymalnie {0} tagów.", MaxTagCount));
            }

            game.Tags = NormalizeStringList(game.Tags, MaxTagCount, lowerCase: true) ?? [];
            var joined = string.Join(",", game.Tags);
            if (joined.Length > MaxCsvFieldLength)
            {
                errors.Add(Loc.F("Lista tagów jest za długa (max {0} znaków po złączeniu).", MaxCsvFieldLength));
            }
        }
    }

    private static string? NormalizeFeatureSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.Trim().ToLowerInvariant()
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        return s switch
        {
            "singleplayer" or "single" => "singleplayer",
            "multiplayer" or "multi" => "multiplayer",
            "coop" or "cooperation" or "cooperative" => "coop",
            "controller" or "controllersupport" or "pad" or "gamepad" => "controller",
            "cloud" or "cloudsaves" => "cloud",
            "achievements" or "achievement" => "achievements",
            "modding" or "mods" or "mod" => "modding",
            "vr" or "virtualreality" => "vr",
            _ => null
        };
    }

    private static bool TryMapFeature(string slug, out GameFeatures flag)
    {
        flag = slug switch
        {
            "singleplayer" => GameFeatures.SinglePlayer,
            "multiplayer" => GameFeatures.MultiPlayer,
            "coop" => GameFeatures.CoOp,
            "controller" => GameFeatures.ControllerSupport,
            "cloud" => GameFeatures.CloudSaves,
            "achievements" => GameFeatures.Achievements,
            "modding" => GameFeatures.Modding,
            "vr" => GameFeatures.Vr,
            _ => GameFeatures.None
        };
        return flag != GameFeatures.None;
    }

    /// <summary>Package-relative icon paths of manifest achievements (http(s) icons are not in the ZIP).</summary>
    public static IEnumerable<string> EnumerateAchievementIconPaths(SgsPackageManifest manifest) =>
        (manifest.Achievements ?? [])
            .Where(a => a is not null && !a.IconIsUrl && !string.IsNullOrWhiteSpace(a.Icon))
            .Select(a => a.Icon!);

    private static void ValidateAchievements(IList<SgsPackageAchievement>? achievements, List<string> errors)
    {
        if (achievements is null)
        {
            return;
        }

        if (achievements.Any(a => a is null))
        {
            errors.Add(Loc.T("Lista 'achievements' zawiera pusty wpis."));
            return;
        }

        foreach (var achievement in achievements.Where(a => !a.IconIsUrl && !string.IsNullOrWhiteSpace(a.Icon)))
        {
            if (TryNormalizeMediaPath(achievement.Icon!, AllowedImageExtensions, $"achievements.{achievement.Key}.icon", out var icon, out var iconError))
            {
                achievement.Icon = icon;
            }
            else
            {
                errors.Add(iconError!);
            }
        }

        // Paths were checked above; the definitions only need the icon to be a well-formed URL or absent.
        var definitions = achievements.Select(a => a.ToDefinition(a.IconIsUrl ? a.Icon : null)).ToList();
        if (AchievementRules.Validate(definitions) is { } error)
        {
            errors.Add(error);
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

        if (!string.IsNullOrWhiteSpace(media.Header))
        {
            if (!TryNormalizeMediaPath(media.Header, AllowedImageExtensions, "header", out var header, out var headerError))
            {
                errors.Add(headerError!);
            }
            else
            {
                media.Header = header;
            }
        }
        else
        {
            media.Header = null;
        }

        if (!string.IsNullOrWhiteSpace(media.Logo))
        {
            if (!TryNormalizeMediaPath(media.Logo, AllowedImageExtensions, "logo", out var logo, out var logoError))
            {
                errors.Add(logoError!);
            }
            else
            {
                media.Logo = logo;
            }
        }
        else
        {
            media.Logo = null;
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
            errors.Add(Loc.F("Maksymalnie {0} screenów.", MaxScreenshots));
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
            error = Loc.F("{0}: niedozwolone rozszerzenie '{1}'. Dozwolone: {2}.", field, ext, string.Join(", ", allowedExtensions));
            return false;
        }

        return true;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SymbolRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 ._-]*$")]
    private static partial Regex SafeStorageSegmentRegex();

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
