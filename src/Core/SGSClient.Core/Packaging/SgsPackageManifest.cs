using SGSClient.Core.DTOs;

namespace SGSClient.Core.Packaging;

/// <summary>
/// Root document for <c>sgs-manifest.json</c> inside an uploaded game ZIP.
/// </summary>
public sealed class SgsPackageManifest
{
    public const int CurrentSchemaVersion = 1;
    public const string FileName = "sgs-manifest.json";

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public required SgsPackageGameInfo Game { get; set; }
    public required SgsPackageBuildInfo Build { get; set; }
    public SgsPackageHints? Package { get; set; }

    /// <summary>
    /// Achievement definitions. Absent = the game's achievements are left as they are; present
    /// (even empty) = this list replaces them on publish.
    /// </summary>
    public IList<SgsPackageAchievement>? Achievements { get; set; }
}

public sealed class SgsPackageAchievement
{
    /// <summary>Stable id used by game code, lowercase letters, digits, _ and -.</summary>
    public required string Key { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Relative path of an image in the ZIP, or an http(s) URL.</summary>
    public string? Icon { get; set; }

    public int Points { get; set; }
    public bool Hidden { get; set; }

    /// <summary>Counter target (2 or more); absent = one-shot achievement.</summary>
    public int? Target { get; set; }

    /// <summary>Title/description per language code, e.g. "en".</summary>
    public IDictionary<string, AchievementTextDto>? Translations { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IconIsUrl =>
        Icon is not null
        && (Icon.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || Icon.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    /// <summary>The definition the server stores; <paramref name="iconUrl"/> is the resolved icon.</summary>
    public AchievementDefinitionDto ToDefinition(string? iconUrl) => new()
    {
        Key = Key,
        Title = Title,
        Description = Description ?? string.Empty,
        IconUrl = iconUrl,
        Translations = Translations is null ? null : new Dictionary<string, AchievementTextDto>(Translations),
        Points = Points,
        Hidden = Hidden,
        Target = Target
    };
}

public sealed class SgsPackageGameInfo
{
    public required string Title { get; set; }
    public required string Symbol { get; set; }
    public string? Description { get; set; }
    public string? HardwareRequirements { get; set; }
    public string? OtherInformation { get; set; }

    /// <summary>game | dlc (default game).</summary>
    public string? Kind { get; set; }

    /// <summary>Parent base-game symbol when <see cref="Kind"/> is dlc.</summary>
    public string? ParentSymbol { get; set; }

    /// <summary>Single genre slug (alias kept for older packages; merged into <see cref="Genres"/>).</summary>
    public string? Genre { get; set; }

    /// <summary>Genre slugs from <see cref="SgsGameTaxonomy.Genres"/> (e.g. action, rpg). Max 5.</summary>
    public IList<string>? Genres { get; set; }

    /// <summary>Single engine slug (alias kept for older packages; merged into <see cref="Engines"/>).</summary>
    public string? Engine { get; set; }

    /// <summary>Engine slugs from <see cref="SgsGameTaxonomy.Engines"/> (e.g. unity). Max 3.</summary>
    public IList<string>? Engines { get; set; }

    /// <summary>Developer / studio display name on the store page.</summary>
    public string? StudioName { get; set; }

    public string? WebsiteUrl { get; set; }
    public string? DiscordUrl { get; set; }
    public string? SupportUrl { get; set; }

    /// <summary>announced | earlyAccess | released | demo</summary>
    public string? ReleaseStatus { get; set; }

    /// <summary>ISO date or DateTimeOffset (e.g. 2024-06-15).</summary>
    public string? ReleaseDate { get; set; }

    /// <summary>unrated | pegi3 | pegi7 | pegi12 | pegi16 | pegi18</summary>
    public string? AgeRating { get; set; }

    /// <summary>
    /// Feature slugs: singleplayer, multiplayer, coop, controller, cloud, achievements, modding, vr.
    /// </summary>
    public IList<string>? Features { get; set; }

    /// <summary>UI languages, e.g. Polski, English.</summary>
    public IList<string>? Languages { get; set; }

    /// <summary>Free-form store tags, e.g. roguelike, pixel-art.</summary>
    public IList<string>? Tags { get; set; }

    public SgsPackageMediaInfo? Media { get; set; }
}

public sealed class SgsPackageMediaInfo
{
    /// <summary>Relative path in the ZIP to the list thumbnail (portrait capsule).</summary>
    public string? Cover { get; set; }

    /// <summary>Relative path in the ZIP to the landscape Steam-style store header.</summary>
    public string? Header { get; set; }

    /// <summary>Relative path in the ZIP to the square game logo (e.g. 512×512).</summary>
    public string? Logo { get; set; }

    /// <summary>Relative path in the ZIP to intro/trailer video.</summary>
    public string? Intro { get; set; }

    /// <summary>Relative paths in the ZIP to screenshot images.</summary>
    public IList<string> Screenshots { get; set; } = [];
}

public sealed class SgsPackageBuildInfo
{
    public required string Version { get; set; }

    /// <summary>windows | linux | macos</summary>
    public required string Platform { get; set; }

    /// <summary>x64 | x86 | arm64</summary>
    public string? Architecture { get; set; } = "x64";

    /// <summary>Relative path inside the ZIP (forward slashes).</summary>
    public required string Executable { get; set; }

    public bool IsDefault { get; set; } = true;

    /// <summary>Changelog for this version, shown in the game's updates tab.</summary>
    public string? ReleaseNotes { get; set; }
}

public sealed class SgsPackageHints
{
    public IList<string> Include { get; set; } = [];
    public IList<string> Exclude { get; set; } = [];
}
