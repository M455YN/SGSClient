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
    [InlineData("app", "app")]
    [InlineData("Dlc", "dlc")]
    [InlineData("book", null)]
    [InlineData("2", null)]
    [InlineData("movie", null)]
    public void NormalizeContentKind_AcceptsKnownKindsOnly(string input, string? expected) =>
        Assert.Equal(expected, SgsPackageRules.NormalizeContentKind(input));

    private static SgsPackageManifest MakeManifest(Action<SgsPackageGameInfo> configure)
    {
        var manifest = new SgsPackageManifest
        {
            Game = new SgsPackageGameInfo { Title = "Demo Runner", Symbol = "demo-runner" },
            Build = new SgsPackageBuildInfo
            {
                Version = "0.1.0",
                Platform = "windows",
                Architecture = "x64",
                Executable = "bin/DemoRunner.exe"
            }
        };
        configure(manifest.Game);
        return manifest;
    }

    [Fact]
    public void ValidateManifest_MergesLegacyAndListGenresAndEngines()
    {
        var manifest = MakeManifest(g =>
        {
            g.Genre = "Action";
            g.Genres = ["rpg", "action"];
            g.Engines = ["unity", "godot"];
        });

        var result = SgsPackageRules.ValidateManifest(manifest);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(["action", "rpg"], manifest.Game.Genres);
        Assert.Equal("action", manifest.Game.Genre);
        Assert.Equal(["unity", "godot"], manifest.Game.Engines);
    }

    [Fact]
    public void ValidateManifest_RejectsTooManyGenresAndEngines()
    {
        var manifest = MakeManifest(g =>
        {
            g.Genres = ["action", "rpg", "puzzle", "racing", "sports", "horror"];
            g.Engines = ["unity", "godot", "unreal", "custom"];
        });

        var result = SgsPackageRules.ValidateManifest(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("gatunków"));
        Assert.Contains(result.Errors, e => e.Contains("silniki"));
    }

    [Fact]
    public void ValidateManifest_RejectsUnknownGenreInList()
    {
        var manifest = MakeManifest(g => g.Genres = ["action", "nope"]);

        var result = SgsPackageRules.ValidateManifest(manifest);

        Assert.Contains(result.Errors, e => e.Contains("nope"));
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

    [Theory]
    [InlineData("games/demo/1.0.0/windows-x64.zip", true)]
    [InlineData("games", false)]                 // the root itself
    [InlineData("games2/demo/x.zip", false)]     // sibling sharing the prefix
    [InlineData("games/../secret.txt", false)]
    public void IsInsideRoot_OnlyAcceptsPathsStrictlyInsideTheRoot(string relative, bool expected)
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "sgs-root-test");
        var root = Path.Combine(baseDir, "games");
        Assert.Equal(expected, SgsPackageRules.IsInsideRoot(root, Path.Combine(baseDir, relative)));
    }

    [Theory]
    [InlineData("1.0.5", true)]
    [InlineData("..", false)]
    [InlineData("../other-game", false)]
    [InlineData(@"..\other-game", false)]
    public void IsSafeStorageSegment_RejectsTraversalInVersion(string version, bool expected) =>
        Assert.Equal(expected, SgsPackageRules.IsSafeStorageSegment(version));

    [Fact]
    public void LobbyFeature_RoundTripsThroughManifestSlug()
    {
        Assert.True(SgsPackageRules.TryParseFeatures(["lobby", "multiplayer"], out var features, out _));
        Assert.Equal(SGSClient.Core.Enums.GameFeatures.Lobby | SGSClient.Core.Enums.GameFeatures.MultiPlayer, features);
        Assert.Contains("lobby", SgsPackageRules.ToManifestFeatures(features));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(1, true)]
    [InlineData(16, true)]
    [InlineData(null, false)]
    [InlineData(0, false)]
    [InlineData(17, false)]
    public void ValidateManifest_LobbyRequiresMaxPlayersInRange(int? maxPlayers, bool expected)
    {
        var manifest = new SgsPackageManifest
        {
            SchemaVersion = 1,
            Game = new SgsPackageGameInfo
            {
                Title = "Lobby Game",
                Symbol = "lobby-game",
                Genre = "action",
                Engine = "custom",
                Features = ["multiplayer", "lobby"],
                LobbyMaxPlayers = maxPlayers
            },
            Build = new SgsPackageBuildInfo { Version = "1.0.0", Platform = "windows", Executable = "Game.exe" }
        };

        var result = SgsPackageRules.ValidateManifest(manifest);
        Assert.Equal(expected, result.IsValid);
    }
}
