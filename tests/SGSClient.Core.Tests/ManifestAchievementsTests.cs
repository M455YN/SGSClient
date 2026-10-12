using SGSClient.Core.Packaging;

namespace SGSClient.Core.Tests;

public sealed class ManifestAchievementsTests
{
    private const string Header = """
        { "schemaVersion": 1,
          "game": { "title": "Demo Runner", "symbol": "demo-runner" },
          "build": { "version": "1.0.0", "platform": "windows", "executable": "bin/Demo.exe" }
        """;

    [Fact]
    public void Achievements_AreParsedValidatedAndConvertedToDefinitions()
    {
        var (manifest, error) = SgsPackageManifestSerializer.TryDeserialize(Header + """
            ,
              "achievements": [
                { "key": "boss", "title": "Boss", "description": "Defeat it", "icon": "media/boss.png", "points": 25,
                  "translations": { "en": { "title": "Slayer", "description": "Defeat the boss" } } },
                { "key": "kills", "title": "Hunter", "target": 100, "hidden": true, "icon": "https://cdn.example/k.png" }
              ] }
            """);

        Assert.Null(error);
        var result = SgsPackageRules.ValidateManifest(manifest);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));

        var boss = manifest!.Achievements![0];
        Assert.Equal("media/boss.png", boss.Icon);
        Assert.Equal(["media/boss.png"], SgsPackageRules.EnumerateAchievementIconPaths(manifest));

        var definition = boss.ToDefinition("/api/games/media/demo-runner/media_boss.png");
        Assert.Equal("Slayer", definition.Translations!["en"].Title);
        Assert.Equal("/api/games/media/demo-runner/media_boss.png", definition.IconUrl);
        Assert.Equal(100, manifest.Achievements[1].Target);
        Assert.True(manifest.Achievements[1].IconIsUrl);
    }

    [Fact]
    public void MissingAchievements_MeansLeaveThemUntouched_WhileEmptyMeansReplace()
    {
        Assert.Null(SgsPackageManifestSerializer.TryDeserialize(Header + " }").Manifest!.Achievements);

        var empty = SgsPackageManifestSerializer.TryDeserialize(Header + """, "achievements": [] }""").Manifest!;
        Assert.NotNull(empty.Achievements);
        Assert.Empty(empty.Achievements);
        Assert.True(SgsPackageRules.ValidateManifest(empty).IsValid);
    }

    [Theory]
    [InlineData("""{ "key": "a", "title": "A" }, { "key": "A", "title": "B" }""")]
    [InlineData("""{ "key": "bad key", "title": "A" }""")]
    [InlineData("""{ "key": "a", "title": "A", "icon": "media/icon.exe" }""")]
    [InlineData("""{ "key": "a", "title": "A", "icon": "../escape.png" }""")]
    [InlineData("""{ "key": "a", "title": "A", "target": 1 }""")]
    [InlineData("""{ "key": "a", "title": "A", "translations": { "english!": { "title": "x" } } }""")]
    public void InvalidAchievements_AreRejected(string items)
    {
        var (manifest, error) = SgsPackageManifestSerializer.TryDeserialize(
            Header + $$""", "achievements": [ {{items}} ] }""");

        Assert.Null(error);
        Assert.False(SgsPackageRules.ValidateManifest(manifest).IsValid);
    }
}
