using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public sealed class RecentGameItemTests
{
    [Fact]
    public void ActivityCard_ShowsPlaytimeArtAndAchievementProgress_OnlyWhenTheyExist()
    {
        var bare = new RecentGameItem("Demo", "dem", lastPlayedUtc: null);

        Assert.False(bare.HasHeader);
        Assert.False(bare.HasAchievements);
        Assert.Equal("Jeszcze nie grano", bare.PlaytimeText);
        Assert.Equal(string.Empty, bare.LastPlayedText);

        var progress = new AchievementSummaryItem
        {
            GameId = 3,
            Title = "Demo",
            ProgressText = "2 / 8",
            PointsText = "20 pkt",
            Fraction = 0.25
        };
        var played = new RecentGameItem(
            "Demo",
            "dem",
            new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            playtimeSeconds: 7200,
            gameId: 3,
            headerImageUrl: "https://example.test/header.jpg",
            achievements: progress);

        Assert.True(played.HasHeader);
        Assert.Same(progress, played.Achievements);
        Assert.StartsWith("2", played.PlaytimeText);
        Assert.StartsWith("Ostatnio grano ", played.LastPlayedText);
    }
}
