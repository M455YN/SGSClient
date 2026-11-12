using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SGSClient.Api.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Services;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Server.Tests;

public class MaintenanceMiddlewareTests
{
    private const string OwnerEmail = "owner@example.com";

    private sealed class Monitor(bool enabled) : IOptionsMonitor<MaintenanceSettings>
    {
        public MaintenanceSettings CurrentValue { get; } = new() { Enabled = enabled };
        public MaintenanceSettings Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<MaintenanceSettings, string?> listener) => null;
    }

    [Theory]
    [InlineData("GET", "/health")]
    [InlineData("GET", "/api/Games")]
    [InlineData("GET", "/api/games/5")]
    [InlineData("GET", "/api/games/5/dlcs")]
    [InlineData("GET", "/api/games/5/builds/2/versions")]
    [InlineData("GET", "/api/games/packages/demo/1.0.0/windows-x64.zip")]
    [InlineData("GET", "/api/games/media/demo/cover.png")]
    [InlineData("GET", "/api/me")]
    [InlineData("GET", "/api/me/library")]
    public void IsAllowed_KeepsWhatADownloadNeeds(string method, string path) =>
        Assert.True(MaintenanceMiddleware.IsAllowed(method, path));

    [Theory]
    [InlineData("GET", "/api/me/games")]
    [InlineData("GET", "/api/games/5/studio/versions")]
    [InlineData("GET", "/api/games/5/studio/achievements")]
    [InlineData("GET", "/api/games/5/stats")]
    [InlineData("GET", "/api/games/5/reviews")]
    [InlineData("PATCH", "/api/games/5")]
    [InlineData("PUT", "/api/games/5/media/cover")]
    [InlineData("POST", "/api/games/uploads")]
    [InlineData("POST", "/api/me/library/5")]
    [InlineData("HEAD", "/api/games/5")]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("GET", "/hubs/social")]
    [InlineData("GET", "/rofw/status")]
    public void IsAllowed_BlocksEverythingElse(string method, string path) =>
        Assert.False(MaintenanceMiddleware.IsAllowed(method, path));

    [Fact]
    public async Task Disabled_PassesEverything()
    {
        var (status, nextCalled, _) = await RunAsync(enabled: false, "POST", "/api/games/uploads", user: null);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task Enabled_AllowedRoutePassesForAnonymous()
    {
        var (_, nextCalled, _) = await RunAsync(enabled: true, "GET", "/api/games", user: null);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Enabled_BlockedRouteReturns503WithRetryAfter()
    {
        var (status, nextCalled, retryAfter) = await RunAsync(
            enabled: true, "GET", "/api/me/games", User("player", "player@example.com"));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        Assert.Equal("300", retryAfter);
    }

    [Fact]
    public async Task Enabled_AnonymousIsBlocked()
    {
        var (status, nextCalled, _) = await RunAsync(enabled: true, "GET", "/api/me/games", user: null);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    [Fact]
    public async Task Enabled_SuperAdminPassesEverything()
    {
        var (_, nextCalled, _) = await RunAsync(
            enabled: true, "POST", "/api/games/uploads", User("owner", OwnerEmail));

        Assert.True(nextCalled);
    }

    private static ClaimsPrincipal User(string sub, string email) =>
        new(new ClaimsIdentity([new Claim("sub", sub), new Claim("email", email), new Claim("name", sub)], "test"));

    private static async Task<(int Status, bool NextCalled, string RetryAfter)> RunAsync(
        bool enabled,
        string method,
        string path,
        ClaimsPrincipal? user)
    {
        // Azure AD on: with it off IsSuperAdmin is always true and nothing would ever be blocked.
        var azure = Options.Create(new AzureAdSettings
        {
            Enabled = true,
            ClientId = "client",
            TenantName = "tenant",
            SigninPolicy = "policy"
        });
        var adminAuth = new AdminAuthorization(Options.Create(new AdminSettings { Emails = [OwnerEmail] }), azure);
        await using var db = new SgsServerDbContext(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var context = new DefaultHttpContext { User = user ?? new ClaimsPrincipal(new ClaimsIdentity()) };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        var sut = new MaintenanceMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new Monitor(enabled),
            NullLogger<MaintenanceMiddleware>.Instance);

        await sut.InvokeAsync(context, new EfUserService(db, adminAuth), adminAuth, azure);
        return (context.Response.StatusCode, nextCalled, context.Response.Headers.RetryAfter.ToString());
    }
}
