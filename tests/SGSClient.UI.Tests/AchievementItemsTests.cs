using SGSClient.Core.DTOs;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

[Collection("Loc")]
public sealed class AchievementItemsTests
{
    private const string Api = "https://api.example/";

    [Fact]
    public void EditorRow_RoundTripsEnglishAndKeepsOtherLanguages()
    {
        var item = StudioAchievementItem.FromDto(
            new AchievementDefinitionDto
            {
                Key = "boss",
                Title = "Pogromca",
                Description = "Pokonaj bossa",
                Points = 25,
                Translations = new Dictionary<string, AchievementTextDto>
                {
                    ["en"] = new() { Title = "Slayer", Description = "Defeat the boss" },
                    ["de"] = new() { Title = "Bezwinger", Description = "Besiege den Boss" }
                }
            },
            Api);

        Assert.Equal("Slayer", item.EditTitleEn);
        item.EditTitleEn = "Boss slayer";
        Assert.True(item.TryToDto(0, out var dto, out var error), error);

        Assert.Equal("Boss slayer", dto!.Translations!["en"].Title);
        Assert.Equal("Bezwinger", dto.Translations["de"].Title);

        item.EditTitleEn = string.Empty;
        item.EditDescriptionEn = string.Empty;
        item.TryToDto(0, out dto, out _);
        Assert.Equal(["de"], dto!.Translations!.Keys);
    }

    [Fact]
    public void EditorRow_ParsesNumbersAndRejectsGarbage()
    {
        var item = new StudioAchievementItem
        {
            EditKey = "kills", EditTitle = "Hunter", EditPoints = " 50 ", EditTarget = "100",
            EditKindIndex = StudioAchievementItem.KindCounter
        };

        Assert.True(item.TryToDto(3, out var dto, out _));
        Assert.Equal((50, 100, 3), (dto!.Points, dto.Target, dto.SortOrder));

        item.EditTarget = "lots";
        Assert.False(item.TryToDto(0, out dto, out var error));
        Assert.Null(dto);
        Assert.Contains("kills", error);

        item.EditTarget = string.Empty;
        Assert.True(item.TryToDto(0, out dto, out _));
        Assert.Null(dto!.Target);
    }

    [Fact]
    public void IconPreview_ResolvesUploadedPathsAgainstTheApi()
    {
        var uploaded = new StudioAchievementItem { ApiBaseUrl = Api, EditIconUrl = "/api/games/media/demo/achievement_boss.png" };
        var external = new StudioAchievementItem { ApiBaseUrl = Api, EditIconUrl = "https://cdn.example/i.png" };
        var none = new StudioAchievementItem { ApiBaseUrl = Api };

        Assert.Equal("https://api.example/api/games/media/demo/achievement_boss.png", uploaded.IconPreviewUrl);
        Assert.Equal("https://cdn.example/i.png", external.IconPreviewUrl);
        Assert.False(none.HasIcon);
    }

    [Fact]
    public void PlayerRow_HidesLockedSecretsAndShowsProgress()
    {
        try
        {
            Loc.Initialize("pl");
            var secret = PlayerAchievementItem.FromDto(
                new GameAchievementDto { Key = "s", Hidden = true, Points = 10, RarityPercent = 2.5 },
                Api);
            var counter = PlayerAchievementItem.FromDto(
                new GameAchievementDto { Key = "k", Title = "Hunter", Target = 100, Progress = 40, Points = 50 },
                Api);
            var done = PlayerAchievementItem.FromDto(
                new GameAchievementDto
                {
                    Key = "d",
                    Title = "Done",
                    UnlockedAtUtc = DateTimeOffset.UtcNow,
                    IconUrl = "/api/games/media/demo/d.png"
                },
                Api);

            Assert.Equal("Ukryte osiągnięcie", secret.Title);
            Assert.False(secret.ShowProgress);
            Assert.Equal("40 / 100", counter.StateText);
            Assert.True(counter.ShowProgress);
            Assert.True(done.IsUnlocked);
            Assert.Equal("https://api.example/api/games/media/demo/d.png", done.IconUrl);
        }
        finally
        {
            Loc.Initialize("pl");
        }
    }

    [Fact]
    public void SummaryRow_ShowsFractionAndFallsBackToGameId()
    {
        var row = AchievementSummaryItem.FromDto(
            new UserAchievementSummaryDto { GameId = 4, GameTitle = "", Unlocked = 3, Total = 12, PointsEarned = 80 });

        Assert.Equal("#4", row.Title);
        Assert.Equal("3 / 12", row.ProgressText);
        Assert.Equal(0.25, row.Fraction);
    }
}
