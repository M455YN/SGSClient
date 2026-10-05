using SGSClient.Core.Packaging;

namespace SGSClient.Core.Tests;

public sealed class SgsPackageRulesTests
{
    [Fact]
    public void ValidateManifest_AcceptsDemoExample()
    {
        var manifest = new SgsPackageManifest
        {
            SchemaVersion = 1,
            Game = new SgsPackageGameInfo
            {
                Title = "Demo Runner",
                Symbol = "demo-runner",
                Description = "Sample",
                Genre = "action",
                Engine = "custom",
                Media = new SgsPackageMediaInfo
                {
                    Cover = "media/cover.png",
                    Screenshots = ["media/screen-01.png"]
                }
            },
            Build = new SgsPackageBuildInfo
            {
                Version = "0.1.0",
                Platform = "windows",
                Architecture = "x64",
                Executable = "bin/DemoRunner.exe",
                IsDefault = true
            }
        };

        var result = SgsPackageRules.ValidateManifest(manifest);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("Demo Runner")]
    [InlineData("UPPER")]
    [InlineData("bad_symbol")]
    [InlineData("a")]
    public void IsValidSymbol_RejectsInvalid(string symbol) =>
        Assert.False(SgsPackageRules.IsValidSymbol(symbol));

    [Fact]
    public void TryNormalizeEntryPath_RejectsTraversal()
    {
        var ok = SgsPackageRules.TryNormalizeEntryPath("../bin/game.exe", out _, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateManifest_RejectsLinuxExeOnWindowsName()
    {
        var manifest = new SgsPackageManifest
        {
            SchemaVersion = 1,
            Game = new SgsPackageGameInfo { Title = "X", Symbol = "cool-game" },
            Build = new SgsPackageBuildInfo
            {
                Version = "1.0.0",
                Platform = "linux",
                Executable = "bin/Game.exe"
            }
        };

        var result = SgsPackageRules.ValidateManifest(manifest);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateManifest_AcceptsStoreMetadata()
    {
        var manifest = new SgsPackageManifest
        {
            SchemaVersion = 1,
            Game = new SgsPackageGameInfo
            {
                Title = "Broken Kayfabe",
                Symbol = "broken-kayfabe",
                Genre = "simulation",
                Engine = "cryengine",
                StudioName = "Ring Dev",
                WebsiteUrl = "https://example.com",
                ReleaseStatus = "early-access",
                ReleaseDate = "2024-06-15",
                AgeRating = "pegi16",
                Features = ["singleplayer", "pad"],
                Languages = ["Polski", "English"],
                Tags = ["wrestling", "RPG"]
            },
            Build = new SgsPackageBuildInfo
            {
                Version = "0.0.2",
                Platform = "windows",
                Executable = "BrokenKayfabe.exe"
            }
        };

        var result = SgsPackageRules.ValidateManifest(manifest);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal("earlyAccess", manifest.Game.ReleaseStatus);
        Assert.Equal("2024-06-15", manifest.Game.ReleaseDate);
        Assert.Equal("pegi16", manifest.Game.AgeRating);
        Assert.Contains("controller", manifest.Game.Features!);
        Assert.Contains("wrestling", manifest.Game.Tags!);
    }
}
