using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Server.Tests;

public class AchievementTokenServiceTests
{
    private const string Key = "0123456789abcdef0123456789abcdef-test-key";

    [Fact]
    public void Issue_ThenValidate_ReturnsTheSameUserAndGame()
    {
        var sut = Create(Key, new MutableTime());

        var session = sut.Issue(userId: 7, gameId: 42);

        Assert.True(sut.TryValidate(session.Token, out var userId, out var gameId));
        Assert.Equal((7, 42), (userId, gameId));
        Assert.Equal(42, session.GameId);
    }

    [Fact]
    public void Validate_RejectsTamperedWrongKeyAndGarbage()
    {
        var sut = Create(Key, new MutableTime());
        var token = sut.Issue(7, 42).Token;
        var parts = token.Split('.');
        var forgedPayload = Convert.ToBase64String("7:43:9999999999"u8.ToArray()).TrimEnd('=');

        Assert.False(sut.TryValidate($"{parts[0]}.{forgedPayload}.{parts[2]}", out _, out _));
        Assert.False(Create(Key + "x", new MutableTime()).TryValidate(token, out _, out _));
        Assert.False(sut.TryValidate(null, out _, out _));
        Assert.False(sut.TryValidate("", out _, out _));
        Assert.False(sut.TryValidate("not-a-token", out _, out _));
        Assert.False(sut.TryValidate("sgs1.@@@.@@@", out _, out _));
    }

    [Fact]
    public void Validate_RejectsExpiredToken()
    {
        var time = new MutableTime();
        var sut = Create(Key, time);
        var token = sut.Issue(7, 42).Token;

        time.Now = time.Now.AddHours(11);
        Assert.True(sut.TryValidate(token, out _, out _));

        time.Now = time.Now.AddHours(2);
        Assert.False(sut.TryValidate(token, out _, out _));
    }

    [Fact]
    public void MissingOrShortKey_StillWorksWithinOneProcess()
    {
        var sut = Create(signingKey: "short", new MutableTime());

        Assert.True(sut.TryValidate(sut.Issue(1, 2).Token, out var userId, out _));
        Assert.Equal(1, userId);
    }

    private static AchievementTokenService Create(string signingKey, TimeProvider time) =>
        new(
            Options.Create(new AchievementSettings { SigningKey = signingKey, TokenLifetimeHours = 12 }),
            NullLogger<AchievementTokenService>.Instance,
            time);

    private sealed class MutableTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
