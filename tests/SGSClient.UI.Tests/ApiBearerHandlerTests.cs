using System.Net;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.Tests;

public sealed class ApiBearerHandlerTests
{
    private static readonly Uri ApiBase = new("https://api.example.com/");

    [Fact]
    public async Task AddsTokenForApiHost()
    {
        var seen = await SendAsync(new FakeAuth(), "https://api.example.com/api/me/games");

        Assert.Equal("Bearer tok", seen.Authorization?.ToString());
    }

    [Fact]
    public async Task SkipsOtherHosts()
    {
        var seen = await SendAsync(new FakeAuth(), "https://cdn.example.net/game.zip");

        Assert.Null(seen.Authorization);
    }

    [Fact]
    public async Task SkipsWhenSignedOut()
    {
        var seen = await SendAsync(new FakeAuth { IsLoggedIn = false }, "https://api.example.com/api/Games");

        Assert.Null(seen.Authorization);
    }

    private static async Task<System.Net.Http.Headers.HttpRequestHeaders> SendAsync(FakeAuth auth, string url)
    {
        var inner = new CaptureHandler();
        var handler = new ApiBearerHandler(auth, ApiBase) { InnerHandler = inner };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync(url);
        return inner.Request!.Headers;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FakeAuth : IAuthSession
    {
        public bool IsAvailable => true;
        public bool IsLoggedIn { get; set; } = true;
        public UserProfileDto? CurrentUser => null;
        public string? LastError => null;
        public Task<bool> TrySilentLoginAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> LoginAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SignUpAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("tok");
        public void SetCurrentUser(UserProfileDto? user)
        {
        }
    }
}
