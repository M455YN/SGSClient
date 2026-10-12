using SGSClient.Core.Achievements;
using SGSClient.Core.DTOs;

namespace SGSClient.Core.Tests;

public class AchievementRulesTests
{
    [Theory]
    [InlineData("first_boss", "first_boss")]
    [InlineData("  First-Boss ", "first-boss")]
    [InlineData("a1", "a1")]
    public void NormalizeKey_TrimsAndLowercases(string raw, string expected) =>
        Assert.Equal(expected, AchievementRules.NormalizeKey(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("zażółć")]
    [InlineData("a.b")]
    public void NormalizeKey_RejectsInvalidKeys(string? raw) =>
        Assert.Null(AchievementRules.NormalizeKey(raw));

    [Fact]
    public void NormalizeKey_RejectsKeysLongerThanTheLimit() =>
        Assert.Null(AchievementRules.NormalizeKey(new string('a', AchievementRules.MaxKeyLength + 1)));

    [Fact]
    public void Validate_AcceptsAWellFormedSet() =>
        Assert.Null(AchievementRules.Validate(
        [
            Def("one"),
            Def("two", target: 5, iconUrl: "https://cdn.example/icon.png"),
            Def("three", points: AchievementRules.MaxPoints, hidden: true)
        ]));

    [Fact]
    public void Validate_FindsTheFirstProblem()
    {
        Assert.NotNull(AchievementRules.Validate([Def("a"), Def("A")]));
        Assert.NotNull(AchievementRules.Validate([Def("a", title: " ")]));
        Assert.NotNull(AchievementRules.Validate([Def("a", points: -1)]));
        Assert.NotNull(AchievementRules.Validate([Def("a", points: AchievementRules.MaxPoints + 1)]));
        Assert.NotNull(AchievementRules.Validate([Def("a", target: 1)]));
        Assert.NotNull(AchievementRules.Validate([Def("a", target: AchievementRules.MaxTarget + 1)]));
        Assert.NotNull(AchievementRules.Validate([Def("a", iconUrl: "ftp://x/y.png")]));
        Assert.NotNull(AchievementRules.Validate(
            Enumerable.Range(0, AchievementRules.MaxPerGame + 1).Select(i => Def($"k{i}")).ToList()));
    }

    private static AchievementDefinitionDto Def(
        string key,
        string title = "Title",
        int points = 10,
        int? target = null,
        string? iconUrl = null,
        bool hidden = false) =>
        new() { Key = key, Title = title, Points = points, Target = target, IconUrl = iconUrl, Hidden = hidden };
}
