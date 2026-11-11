using SGSClient.Core.Achievements;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Packaging;

namespace SGSClient.Core.Tests;

public sealed class AchievementStatRulesTests
{
    [Theory]
    [InlineData(0, 10, 0, false)]
    [InlineData(9.9, 10, 9, false)]
    [InlineData(10, 10, 10, true)]
    [InlineData(250, 10, 10, true)]
    [InlineData(-5, 10, 0, false)]
    public void StatProgress_FloorsClampsAndUnlocksAtTheThreshold(double value, int target, int progress, bool reached) =>
        Assert.Equal((progress, reached), AchievementRules.StatProgress(value, target));

    [Fact]
    public void CheckStatValue_EnforcesTypeRangeIncrementOnlyAndMaxChange()
    {
        var stat = new GameStatDefinitionDto
        {
            Key = "num_games",
            Type = GameStatType.Int,
            MinValue = 0,
            MaxValue = 100,
            MaxChange = 10,
            IncrementOnly = true
        };

        Assert.Null(AchievementRules.CheckStatValue(stat, 5, 15));
        Assert.NotNull(AchievementRules.CheckStatValue(stat, 5, 6.5)); // INT takes whole numbers only
        Assert.NotNull(AchievementRules.CheckStatValue(stat, 95, 101)); // above max
        Assert.NotNull(AchievementRules.CheckStatValue(stat, 5, 4)); // increment only
        Assert.NotNull(AchievementRules.CheckStatValue(stat, 5, 16)); // max change 10
        Assert.NotNull(AchievementRules.CheckStatValue(stat, 5, double.NaN));

        var distance = new GameStatDefinitionDto { Key = "distance", Type = GameStatType.Float };
        Assert.Null(AchievementRules.CheckStatValue(distance, 3.5, 1.25)); // no limits: may go down
    }

    [Fact]
    public void ValidateStats_RejectsBadDefinitions()
    {
        Assert.Null(AchievementRules.ValidateStats([Stat("NUM_GAMES"), Stat("distance", GameStatType.Float, 0.5)]));
        Assert.NotNull(AchievementRules.ValidateStats([Stat("a"), Stat("A")])); // duplicate after lowercasing
        Assert.NotNull(AchievementRules.ValidateStats([Stat("bad key")]));
        Assert.NotNull(AchievementRules.ValidateStats([Stat("frac", GameStatType.Int, 0.5)])); // INT default must be whole
        Assert.NotNull(AchievementRules.ValidateStats([new GameStatDefinitionDto { Key = "r", MinValue = 5, MaxValue = 1 }]));
        Assert.NotNull(AchievementRules.ValidateStats([new GameStatDefinitionDto { Key = "d", MaxValue = 1, DefaultValue = 2 }]));
        Assert.NotNull(AchievementRules.ValidateStats([new GameStatDefinitionDto { Key = "m", MaxChange = 0 }]));
    }

    [Fact]
    public void Validate_LinkedStatMustExistAndNeedsATarget()
    {
        AchievementDefinitionDto Linked(int? target) =>
            new() { Key = "ACH_WIN_100_GAMES", Title = "Champion", StatKey = "NUM_GAMES", Target = target };

        Assert.Null(AchievementRules.Validate([Linked(100)], ["num_games"]));
        Assert.Null(AchievementRules.Validate([Linked(100)])); // no stat list = existence not checked
        Assert.NotNull(AchievementRules.Validate([Linked(100)], ["other"]));
        Assert.NotNull(AchievementRules.Validate([Linked(null)], ["num_games"]));
    }

    [Fact]
    public void Manifest_CarriesLockedIconsAndStats_AndMustDeclareLinkedStats()
    {
        const string header = """
            { "schemaVersion": 1,
              "game": { "title": "Demo Runner", "symbol": "demo-runner" },
              "build": { "version": "1.0.0", "platform": "windows", "executable": "bin/Demo.exe" },
              "achievements": [
                { "key": "ACH_WIN_100_GAMES", "title": "Champion", "icon": "media/win.png",
                  "lockedIcon": "media/win_gray.png", "stat": "NUM_GAMES", "target": 100 } ]
            """;

        var (withStats, error) = SgsPackageManifestSerializer.TryDeserialize(header + """
            , "stats": [ { "key": "NUM_GAMES", "displayName": "Games won", "type": "Int", "incrementOnly": true } ] }
            """);
        Assert.Null(error);
        var result = SgsPackageRules.ValidateManifest(withStats);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal(["media/win.png", "media/win_gray.png"], SgsPackageRules.EnumerateAchievementIconPaths(withStats!));
        Assert.Equal(GameStatType.Int, withStats!.Stats![0].Type);
        Assert.Equal("NUM_GAMES", withStats.Achievements![0].ToDefinition(null, "/x.png").StatKey);

        var withoutStats = SgsPackageManifestSerializer.TryDeserialize(header + " }").Manifest;
        Assert.False(SgsPackageRules.ValidateManifest(withoutStats).IsValid);
    }

    [Theory]
    [InlineData("Pogromca bossa!", "ach_pogromca_bossa")]
    [InlineData("Zażółć gęślą jaźń", "ach_zazolc_gesla_jazn")]
    [InlineData("  Win 100 games  ", "ach_win_100_games")]
    [InlineData("", "")]
    [InlineData("!!!", "")]
    public void SuggestKey_MakesAValidApiNameFromTheTitle(string title, string expected)
    {
        var key = AchievementRules.SuggestKey(title);
        Assert.Equal(expected, key);
        Assert.True(key.Length == 0 || AchievementRules.NormalizeKey(key) == key);
    }

    [Fact]
    public void IconConvention_FillsMissingIconsFromTheFolder_AndKeepsExplicitOnes()
    {
        var manifest = new SgsPackageManifest
        {
            Game = new SgsPackageGameInfo { Title = "Demo", Symbol = "demo" },
            Build = new SgsPackageBuildInfo { Version = "1", Platform = "windows", Executable = "a.exe" },
            Achievements =
            [
                new SgsPackageAchievement { Key = "ACH_BOSS", Title = "Boss" },
                new SgsPackageAchievement { Key = "ach_run", Title = "Run", Icon = "media/run.png" }
            ]
        };

        SgsPackageRules.ApplyAchievementIconConvention(manifest,
        [
            "sdk_sgs/achievements/ach_boss.PNG",
            "sdk_sgs/achievements/ACH_BOSS_locked.jpg",
            "sdk_sgs/achievements/ach_run.png",
            "sdk_sgs/achievements/old/ach_run_locked.png",
            "bin/Demo.exe"
        ]);

        Assert.Equal("sdk_sgs/achievements/ach_boss.PNG", manifest.Achievements[0].Icon);
        Assert.Equal("sdk_sgs/achievements/ACH_BOSS_locked.jpg", manifest.Achievements[0].LockedIcon);
        Assert.Equal("media/run.png", manifest.Achievements[1].Icon); // explicit wins
        Assert.Null(manifest.Achievements[1].LockedIcon); // sub-folders are not part of the convention
    }

    [Fact]
    public void Export_MapsUploadedIconsToTheGameFolder_AndKeepsExternalUrls()
    {
        var entry = SgsPackageManifestFactory.ToPackageAchievement(new AchievementDefinitionDto
        {
            Key = "ach_win_10",
            Title = "Ten",
            IconUrl = "/api/games/media/demo/sdk_sgs/achievements/ach_win_10.jpg",
            LockedIconUrl = "https://cdn.example/gray.png",
            StatKey = "num_games",
            Target = 10
        });

        Assert.Equal("sdk_sgs/achievements/ach_win_10.jpg", entry.Icon);
        Assert.Equal("https://cdn.example/gray.png", entry.LockedIcon);
        Assert.Equal(("num_games", (int?)10), (entry.Stat, entry.Target));
        Assert.Equal("sdk_sgs/achievements/ach_win_10_locked.png",
            SgsPackageManifestFactory.LocalIconPath("ach_win_10", "/api/games/media/demo/achievement_x.png", locked: true));
    }

    private static GameStatDefinitionDto Stat(string key, GameStatType type = GameStatType.Int, double defaultValue = 0) =>
        new() { Key = key, Type = type, DefaultValue = defaultValue };
}
