using System.Text.Json;
using SGSClient.Core.Achievements;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;

namespace SGSClient.Core.Packaging;

/// <summary>Builds an <see cref="SgsPackageManifest"/> from a game already in the catalog.</summary>
public static class SgsPackageManifestFactory
{
    /// <summary>
    /// Catalog fields plus the default build. Media and achievements are left out on purpose:
    /// a republish keeps the current ones unless the manifest lists new files. The "download manifest"
    /// action adds achievements with <see cref="ToPackageAchievement"/>.
    /// </summary>
    public static SgsPackageManifest FromGame(GameDto game)
    {
        var build = game.Builds.FirstOrDefault(b => b.IsDefault) ?? game.Builds.FirstOrDefault();

        return new SgsPackageManifest
        {
            Game = new SgsPackageGameInfo
            {
                Title = game.Title,
                Symbol = game.Symbol,
                Kind = game.Kind.ToString().ToLowerInvariant(),
                ParentSymbol = game.Kind == GameContentKind.Dlc ? game.ParentSymbol : null,
                Description = game.Description,
                HardwareRequirements = game.HardwareRequirements,
                OtherInformation = game.OtherInformation,
                Genres = game.Genres.Select(g => g.Symbol).ToList(),
                Engines = game.Engines.Select(e => e.Symbol).ToList(),
                StudioName = game.StudioName,
                WebsiteUrl = game.WebsiteUrl,
                DiscordUrl = game.DiscordUrl,
                SupportUrl = game.SupportUrl,
                ReleaseStatus = SgsPackageRules.ToManifestReleaseStatus(game.ReleaseStatus),
                ReleaseDate = game.ReleaseDateUtc?.ToString("yyyy-MM-dd"),
                AgeRating = SgsPackageRules.ToManifestAgeRating(game.AgeRating),
                Features = SgsPackageRules.ToManifestFeatures(game.Features).ToList(),
                Languages = game.Languages.ToList(),
                Tags = game.Tags.ToList()
            },
            Build = new SgsPackageBuildInfo
            {
                Version = build?.CurrentVersion ?? string.Empty,
                Platform = SgsPackageRules.ToManifestPlatform(build?.Platform ?? GamePlatform.Windows),
                Architecture = build?.Architecture ?? "x64",
                Executable = build?.ExeName ?? string.Empty,
                IsDefault = true
            }
        };
    }

    /// <summary>
    /// The manifest entry for a stored achievement. Uploaded icons map to their file in the game folder
    /// (<see cref="LocalIconPath"/>); external http(s) icons stay URLs.
    /// </summary>
    public static SgsPackageAchievement ToPackageAchievement(AchievementDefinitionDto definition) => new()
    {
        Key = definition.Key,
        Title = definition.Title,
        Description = string.IsNullOrEmpty(definition.Description) ? null : definition.Description,
        Icon = LocalIconPath(definition.Key, definition.IconUrl, locked: false) ?? definition.IconUrl,
        LockedIcon = LocalIconPath(definition.Key, definition.LockedIconUrl, locked: true) ?? definition.LockedIconUrl,
        Stat = definition.StatKey,
        Points = definition.Points,
        Hidden = definition.Hidden,
        Target = definition.Target,
        Translations = definition.Translations is null ? null : new Dictionary<string, AchievementTextDto>(definition.Translations)
    };

    /// <summary>
    /// "sdk_sgs/achievements/{key}[_locked]{ext}" for an icon uploaded to SGS (an app-relative URL);
    /// null for no icon or an external http(s) one.
    /// </summary>
    public static string? LocalIconPath(string key, string? iconUrl, bool locked)
    {
        if (string.IsNullOrWhiteSpace(iconUrl) || SgsPackageAchievement.IsUrl(iconUrl))
        {
            return null;
        }

        var extension = Path.GetExtension(iconUrl.Split('?', '#')[0]).ToLowerInvariant();
        if (!SgsPackageRules.AllowedImageExtensions.Contains(extension))
        {
            extension = ".png";
        }

        return $"{AchievementRules.IconFolder}/{key}{(locked ? "_locked" : string.Empty)}{extension}";
    }

    public static string ToJson(SgsPackageManifest manifest) =>
        JsonSerializer.Serialize(manifest, SgsPackageManifestSerializer.Options);
}
