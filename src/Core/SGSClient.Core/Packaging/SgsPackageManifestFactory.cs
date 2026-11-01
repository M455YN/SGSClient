using System.Text.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;

namespace SGSClient.Core.Packaging;

/// <summary>Builds an <see cref="SgsPackageManifest"/> from a game already in the catalog.</summary>
public static class SgsPackageManifestFactory
{
    /// <summary>
    /// Catalog fields plus the default build. Media and achievements are left out on purpose:
    /// a republish keeps the current ones unless the manifest lists new files.
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

    public static string ToJson(SgsPackageManifest manifest) =>
        JsonSerializer.Serialize(manifest, SgsPackageManifestSerializer.Options);
}
