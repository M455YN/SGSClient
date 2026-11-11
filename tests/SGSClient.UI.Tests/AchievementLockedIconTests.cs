using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

[Collection("Loc")]
public sealed class AchievementLockedIconTests
{
    private const string Api = "https://api.example/";

    [Fact]
    public void Player_LockedShowsTheUnachievedIcon_UnlockedShowsTheAchievedOne()
    {
        var locked = PlayerAchievementItem.FromDto(Dto(unlocked: false, lockedIcon: "/gray.png"), Api);
        var unlocked = PlayerAchievementItem.FromDto(Dto(unlocked: true, lockedIcon: "/gray.png"), Api);

        Assert.Equal("https://api.example/gray.png", locked.IconUrl);
        Assert.Equal(1, locked.IconOpacity);
        Assert.Equal("https://api.example/color.png", unlocked.IconUrl);
        Assert.Equal(1, unlocked.IconOpacity);
    }

    [Fact]
    public void Player_LockedWithoutUnachievedIcon_DimsTheAchievedOne()
    {
        var item = PlayerAchievementItem.FromDto(Dto(unlocked: false, lockedIcon: null), Api);

        Assert.Equal("https://api.example/color.png", item.IconUrl);
        Assert.True(item.IconOpacity < 1);
    }

    [Fact]
    public void Player_HiddenAndLocked_IsMaskedWithoutIconOrProgress()
    {
        var item = PlayerAchievementItem.FromDto(
            new GameAchievementDto { Key = "secret", Hidden = true, Target = 10, Progress = 4 }, Api);

        Assert.False(item.HasIcon);
        Assert.False(item.ShowProgress);
        Assert.NotEqual(string.Empty, item.Title);
        Assert.DoesNotContain("4", item.StateText);
    }

    [Fact]
    public void EditorRow_ShowsStatThresholdAndRoundTripsTheNewFields()
    {
        var item = StudioAchievementItem.FromDto(
            new AchievementDefinitionDto { Key = "ach_win_10", Title = "Ten", Target = 10, StatKey = "num_games", LockedIconUrl = "/g.png" },
            Api);

        Assert.Equal("num_games ≥ 10", item.ProgressText);
        Assert.Equal(1, item.LockedColumnOpacity);
        Assert.True(item.TryToDto(0, out var dto, out _));
        Assert.Equal(("num_games", "/g.png"), (dto!.StatKey, dto.LockedIconUrl));
    }

    [Fact]
    public void StatRow_ParsesCommaDecimalsAndEmptyLimits()
    {
        var row = new StudioStatItem { EditKey = "distance", EditTypeIndex = 1, EditMin = "", EditMax = "1,5", EditDefault = "0.25" };

        Assert.True(row.TryToDto(3, out var dto, out var error), error);
        Assert.Equal((GameStatType.Float, (double?)null, (double?)1.5, 0.25, 3), (dto!.Type, dto.MinValue, dto.MaxValue, dto.DefaultValue, dto.SortOrder));

        row.EditMax = "abc";
        Assert.False(row.TryToDto(0, out _, out _));
    }

    private static GameAchievementDto Dto(bool unlocked, string? lockedIcon) => new()
    {
        Key = "boss",
        Title = "Boss",
        IconUrl = "/color.png",
        LockedIconUrl = lockedIcon,
        UnlockedAtUtc = unlocked ? DateTimeOffset.UtcNow : null
    };
}
