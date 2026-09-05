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

    /// <summary>Genre slug from <see cref="SgsGameTaxonomy.Genres"/> (e.g. action).</summary>
    public string? Genre { get; set; }

    /// <summary>Engine slug from <see cref="SgsGameTaxonomy.Engines"/> (e.g. unity).</summary>
    public string? Engine { get; set; }

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
