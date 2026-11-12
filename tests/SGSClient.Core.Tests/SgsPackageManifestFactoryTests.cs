using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Packaging;

namespace SGSClient.Core.Tests;

public sealed class SgsPackageManifestFactoryTests
{
    [Fact]
    public void FromGame_RoundTripsThroughJsonAndPassesValidation()
    {
        var game = new GameDto
        {
            Title = "Demo Runner",
            Symbol = "demo-runner",
            Description = "Run.",
            Genres = [SgsGameTaxonomy.Genres[0]],
            StudioName = "Studio",
            ReleaseStatus = GameReleaseStatus.EarlyAccess,
            ReleaseDateUtc = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero),
            AgeRating = GameAgeRating.Pegi12,
            Features = GameFeatures.SinglePlayer | GameFeatures.CoOp,
            LanguagesCsv = "Polski, English",
            Builds =
            [
                new GameBuildDto
                {
                    Platform = GamePlatform.Windows,
                    Architecture = "x64",
                    CurrentVersion = "1.2.0",
                    ExeName = "bin/DemoRunner.exe",
                    ZipLink = "x",
                    IsDefault = true
                }
            ]
        };

        var json = SgsPackageManifestFactory.ToJson(SgsPackageManifestFactory.FromGame(game));
        var (manifest, error) = SgsPackageManifestSerializer.TryDeserialize(json);

        Assert.Null(error);
        Assert.NotNull(manifest);
        Assert.True(SgsPackageRules.ValidateManifest(manifest).IsValid);
        Assert.Equal("1.2.0", manifest.Build.Version);
        Assert.Equal("bin/DemoRunner.exe", manifest.Build.Executable);
        Assert.Equal("earlyAccess", manifest.Game.ReleaseStatus);
        Assert.Equal("2024-06-15", manifest.Game.ReleaseDate);
        Assert.Equal(["singleplayer", "coop"], manifest.Game.Features);
        Assert.Equal(["Polski", "English"], manifest.Game.Languages);
        Assert.Null(manifest.Game.Media);
    }
}
