using System.Text.Json;
using SGSClient.Core.Achievements;
using SGSClient.Core.Packaging;

namespace SGSClient.Core.Tests;

/// <summary>
/// The published JSON schema (used by creators' editors) and the C# rules must agree.
/// These tests fail when somebody edits one side only.
/// </summary>
public sealed class SchemaDriftTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static JsonElement GameProps => LoadSchema().GetProperty("properties").GetProperty("game").GetProperty("properties");
    private static JsonElement BuildProps => LoadSchema().GetProperty("properties").GetProperty("build").GetProperty("properties");

    [Fact]
    public void GenreAndEngineEnums_MatchTaxonomy()
    {
        var genres = SgsGameTaxonomy.Genres.Select(g => g.Symbol);
        var engines = SgsGameTaxonomy.Engines.Select(e => e.Symbol);

        AssertSameSet(genres, Enum(GameProps.GetProperty("genre")));
        AssertSameSet(genres, Enum(GameProps.GetProperty("genres").GetProperty("items")));
        AssertSameSet(engines, Enum(GameProps.GetProperty("engine")));
        AssertSameSet(engines, Enum(GameProps.GetProperty("engines").GetProperty("items")));
    }

    [Fact]
    public void FeatureStatusAndAgeEnums_MatchRules()
    {
        AssertSameSet(SgsPackageRules.FeatureSlugs, Enum(GameProps.GetProperty("features").GetProperty("items")));
        AssertSameSet(SgsPackageRules.ReleaseStatusSlugs, Enum(GameProps.GetProperty("releaseStatus")));
        AssertSameSet(SgsPackageRules.AgeRatingSlugs, Enum(GameProps.GetProperty("ageRating")));
    }

    [Fact]
    public void Limits_MatchRules()
    {
        var game = GameProps;
        Assert.Equal(SgsPackageRules.MinTitleLength, game.GetProperty("title").GetProperty("minLength").GetInt32());
        Assert.Equal(SgsPackageRules.MaxTitleLength, game.GetProperty("title").GetProperty("maxLength").GetInt32());
        Assert.Equal(SgsPackageRules.MaxSymbolLength, game.GetProperty("symbol").GetProperty("maxLength").GetInt32());
        Assert.Equal(SgsPackageRules.MaxDescriptionLength, game.GetProperty("description").GetProperty("maxLength").GetInt32());
        Assert.Equal(SgsPackageRules.MaxStudioNameLength, game.GetProperty("studioName").GetProperty("maxLength").GetInt32());
        Assert.Equal(SgsPackageRules.MaxUrlLength, game.GetProperty("websiteUrl").GetProperty("maxLength").GetInt32());
        Assert.Equal(SgsPackageRules.MaxFeatureCount, game.GetProperty("features").GetProperty("maxItems").GetInt32());
        Assert.Equal(SgsPackageRules.MaxLanguageCount, game.GetProperty("languages").GetProperty("maxItems").GetInt32());
        Assert.Equal(SgsPackageRules.MaxTagCount, game.GetProperty("tags").GetProperty("maxItems").GetInt32());
        Assert.Equal(SgsGameTaxonomy.MaxGenres, game.GetProperty("genres").GetProperty("maxItems").GetInt32());
        Assert.Equal(SgsGameTaxonomy.MaxEngines, game.GetProperty("engines").GetProperty("maxItems").GetInt32());
        Assert.Equal(
            SgsPackageRules.MaxScreenshots,
            game.GetProperty("media").GetProperty("properties").GetProperty("screenshots").GetProperty("maxItems").GetInt32());

        var build = BuildProps;
        Assert.Equal(SgsPackageRules.MaxVersionLength, build.GetProperty("version").GetProperty("maxLength").GetInt32());
        Assert.Equal(SgsPackageRules.MaxExecutablePathLength, build.GetProperty("executable").GetProperty("maxLength").GetInt32());
        Assert.Equal(SgsPackageRules.MaxReleaseNotesLength, build.GetProperty("releaseNotes").GetProperty("maxLength").GetInt32());
    }

    [Fact]
    public void AchievementLimits_MatchRules()
    {
        var root = LoadSchema().GetProperty("properties").GetProperty("achievements");
        var item = root.GetProperty("items").GetProperty("properties");

        Assert.Equal(AchievementRules.MaxPerGame, root.GetProperty("maxItems").GetInt32());
        Assert.Equal(AchievementRules.MaxKeyLength, item.GetProperty("key").GetProperty("maxLength").GetInt32());
        Assert.Equal(AchievementRules.MaxTitleLength, item.GetProperty("title").GetProperty("maxLength").GetInt32());
        Assert.Equal(AchievementRules.MaxDescriptionLength, item.GetProperty("description").GetProperty("maxLength").GetInt32());
        Assert.Equal(AchievementRules.MaxPoints, item.GetProperty("points").GetProperty("maximum").GetInt32());
        Assert.Equal(AchievementRules.MinTarget, item.GetProperty("target").GetProperty("minimum").GetInt32());
        Assert.Equal(AchievementRules.MaxTarget, item.GetProperty("target").GetProperty("maximum").GetInt32());
        Assert.Equal(AchievementRules.MaxTranslations, item.GetProperty("translations").GetProperty("maxProperties").GetInt32());
    }

    [Fact]
    public void ShippedExampleManifests_PassValidation()
    {
        var files = Directory
            .EnumerateFiles(Path.Combine(RepoRoot, "schemas", "examples"), "*.json", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).EndsWith("sgs-manifest.json", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var (manifest, error) = SgsPackageManifestSerializer.TryDeserialize(File.ReadAllText(file));
            Assert.True(error is null, $"{file}: {error}");

            // catalog-templates ship placeholder executables ("Game" instead of "Game.exe"): see their README.
            var isTemplate = file.Contains("catalog-templates", StringComparison.OrdinalIgnoreCase);
            var errors = SgsPackageRules.ValidateManifest(manifest).Errors
                .Where(e => !(isTemplate && e.StartsWith("Plik uruchomieniowy nie pasuje", StringComparison.Ordinal)))
                .ToList();
            Assert.True(errors.Count == 0, $"{file}: {string.Join("; ", errors)}");
        }
    }

    private static IEnumerable<string> Enum(JsonElement element) =>
        element.GetProperty("enum").EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!);

    private static void AssertSameSet(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var e = expected.OrderBy(x => x, StringComparer.Ordinal).ToList();
        var a = actual.OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(e, a);
    }

    private static JsonElement LoadSchema()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "schemas", "sgs-manifest.v1.json")));
        return doc.RootElement.Clone();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "schemas", "sgs-manifest.v1.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("schemas/sgs-manifest.v1.json not found above " + AppContext.BaseDirectory);
    }
}
