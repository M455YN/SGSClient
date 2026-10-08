using SGSClient.Core.Enums;

namespace SGSClient.Core.Models;

public sealed class GameLibraryItem
{
    public int GameId { get; init; }
    public int BuildId { get; init; }
    public required string Symbol { get; init; }
    public required string Title { get; set; }
    public GamePlatform Platform { get; set; }
    public string? Architecture { get; set; }
    public InstallState InstallState { get; set; } = InstallState.NotInstalled;
    public string? InstallPath { get; set; }
    public string? ExecutablePath { get; set; }
    public string? InstalledVersion { get; set; }

    /// <summary>
    /// Version selected in game properties (Steam-like). Null/empty = use latest/current on install.
    /// </summary>
    public string? TargetVersion { get; set; }

    /// <summary>Optional capsule/cover art URL from the store catalog.</summary>
    public string? CoverUrl { get; set; }

    /// <summary>Optional square logo URL for sidebar tiles; falls back to <see cref="CoverUrl"/>.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Optional local path to a user-chosen library tile image. Overrides CoverUrl in the library UI.</summary>
    public string? CustomCoverPath { get; set; }

    public DateTimeOffset? LastPlayedUtc { get; set; }

    /// <summary>User-scoped total playtime. Overlay from <c>IPlaytimeStore</c>; not persisted on the install row.</summary>
    public long TotalPlayTimeSeconds { get; set; }

    /// <summary>
    /// Copies the portrait capsule and square logo from the catalog when the local row is missing them
    /// or still points at the landscape header.
    /// </summary>
    public bool ApplyStoreArtwork(string? portraitUrl, string? logoUrl, string? headerUrl)
    {
        var changed = false;
        if (!string.IsNullOrWhiteSpace(portraitUrl)
            && (string.IsNullOrWhiteSpace(CoverUrl) || CoverUrl == headerUrl))
        {
            CoverUrl = portraitUrl;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(logoUrl) && LogoUrl != logoUrl)
        {
            LogoUrl = logoUrl;
            changed = true;
        }

        return changed;
    }
}
