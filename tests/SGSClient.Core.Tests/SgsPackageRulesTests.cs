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
}
