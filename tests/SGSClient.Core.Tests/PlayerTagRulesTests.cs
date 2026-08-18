using SGSClient.Core.Auth;

namespace SGSClient.Core.Tests;

public sealed class PlayerTagRulesTests
{
    [Theory]
    [InlineData("m455yn")]
    [InlineData("Gracz_01")]
    [InlineData("a.b_c")]
    public void TryNormalize_AcceptsValidTags(string raw)
    {
        Assert.True(PlayerTagRules.TryNormalize(raw, out var tag, out var error));
        Assert.Equal(raw.Trim().TrimStart('@').ToLowerInvariant(), tag);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("admin")]
    [InlineData(".hidden")]
    [InlineData("ends.")]
    [InlineData("has..dots")]
    [InlineData("sp ace")]
    public void TryNormalize_RejectsInvalidTags(string raw)
    {
        Assert.False(PlayerTagRules.TryNormalize(raw, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void SuggestFrom_UsesEmailWhenDisplayNameIsGeneric()
    {
        var tag = PlayerTagRules.SuggestFrom("Player", "m455yn@example.com");
        Assert.Equal("m455yn", tag);
    }

    [Fact]
    public void SuggestFrom_PadsShortNames()
    {
        var tag = PlayerTagRules.SuggestFrom("ab", null);
        Assert.NotNull(tag);
        Assert.True(tag!.Length >= PlayerTagRules.MinLength);
    }
}
