using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.Tests;

/// <summary>The achievement bridge: what a launched game gets, what is relayed and what is queued.</summary>
public sealed class GameLauncherEnvironmentTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sgs-bridge-{Guid.NewGuid():N}");
    private readonly ScriptedApi _api = new();
    private readonly List<AchievementBridge> _bridges = [];

    public GameLauncherEnvironmentTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var bridge in _bridges)
        {
            bridge.Dispose();
        }

        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // a script may still hold the folder; the temp dir is cleaned by the OS eventually
        }
    }

    [Fact]
    public async Task Launch_HandsTheGameTheBridgeAddressAndAPerLaunchToken()
    {
        var bridge = CreateBridge();
        var output = await LaunchAsync(bridge, gameId: 7);

        var parts = output.Split(';');
        Assert.Equal("7", parts[0]);
        Assert.Equal(bridge.Start(), parts[1]);
        Assert.Matches("^[0-9a-f]{32}$", parts[2]);
    }

    [Fact]
    public async Task Unlock_IsRelayedToTheApiWithThePlayersOwnSignIn()
    {
        var (bridge, token) = StartSession(gameId: 7);
        _api.Respond(HttpStatusCode.OK, """{"key":"boss","progress":1,"unlocked":true,"justUnlocked":true}""");

        var (status, body) = await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/Boss/unlock");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("justUnlocked", body);
        var sent = Assert.Single(_api.Requests);
        Assert.Equal("https://api.example/api/me/games/7/achievements/boss/unlock", sent.Url);
        Assert.Equal("Bearer player-token", sent.Authorization);
        Assert.Equal(0, bridge.PendingCount);
    }

    [Fact]
    public async Task Offline_CallsAreQueuedAcknowledgedAndReplayedLater()
    {
        var (bridge, token) = StartSession(gameId: 7);
        _api.Offline = true;

        var (status, body) = await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/boss/unlock");
        await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/boss/unlock");
        await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/kills/progress", """{"value":5}""");
        await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/kills/progress", """{"value":3}""");

        Assert.Equal(HttpStatusCode.Accepted, status);
        Assert.Contains("\"queued\": true", body);
        Assert.Equal(2, bridge.PendingCount); // one unlock, one progress (the highest value wins)

        _api.Offline = false;
        _api.Respond(HttpStatusCode.OK, "{}");
        await bridge.FlushAsync();

        Assert.Equal(0, bridge.PendingCount);
        Assert.Equal(
            [
                "https://api.example/api/me/games/7/achievements/boss/unlock",
                "https://api.example/api/me/games/7/achievements/kills/progress"
            ],
            _api.Requests.Select(r => r.Url));
        Assert.Contains("\"value\":5", _api.Requests[1].Body);
    }

    [Fact]
    public async Task QueuedCalls_SurviveARestartOfTheClient()
    {
        var queue = Path.Combine(_dir, "persisted.json");
        var (first, token) = StartSession(gameId: 7, queuePath: queue);
        _api.Offline = true;
        await CallAsync(first, token, HttpMethod.Post, "/api/sdk/achievements/boss/unlock");
        first.Dispose();

        var second = CreateBridge(queue);

        Assert.Equal(1, second.PendingCount);
    }

    [Fact]
    public async Task DefinitiveApiAnswers_AreRelayedAndNeverQueued()
    {
        var (bridge, token) = StartSession(gameId: 7);
        _api.Respond(HttpStatusCode.NotFound, """{"errors":["Osiągnięcie nie istnieje."]}""");

        var (status, _) = await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/nope/unlock");

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal(0, bridge.PendingCount);
    }

    [Fact]
    public async Task UnknownTokenAndBadRequests_AreRejected()
    {
        var (bridge, token) = StartSession(gameId: 7);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await CallAsync(bridge, "nope", HttpMethod.Post, "/api/sdk/achievements/boss/unlock")).Status);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/kills/progress", "not json")).Status);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/kills/progress", """{"value":-1}""")).Status);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await CallAsync(bridge, token, HttpMethod.Post, "/api/sdk/achievements/bad key/unlock")).Status);
        Assert.Empty(_api.Requests);
    }

    private (AchievementBridge Bridge, string Token) StartSession(int gameId, string? queuePath = null)
    {
        var bridge = CreateBridge(queuePath);
        var env = bridge.CreateSession(gameId)!;
        return (bridge, env["SGS_ACHIEVEMENTS_TOKEN"]);
    }

    private AchievementBridge CreateBridge(string? queuePath = null)
    {
        var bridge = new AchievementBridge(
            new SignedInAuth(),
            Options.Create(new ApiClientSettings { BaseUrl = "https://api.example/" }),
            _api,
            queuePath ?? Path.Combine(_dir, $"queue-{Guid.NewGuid():N}.json"),
            flushInterval: TimeSpan.FromHours(1));
        _bridges.Add(bridge);
        return bridge;
    }

    private static async Task<(HttpStatusCode Status, string Body)> CallAsync(
        AchievementBridge bridge,
        string token,
        HttpMethod method,
        string path,
        string? json = null)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(method, bridge.Start() + path);
        request.Headers.Add(AchievementBridge.TokenHeader, token);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await http.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Starts a real throw-away script and reads back the environment the launcher gave it.</summary>
    private async Task<string> LaunchAsync(AchievementBridge bridge, int gameId)
    {
        var outputPath = Path.Combine(_dir, "env.txt");
        var script = OperatingSystem.IsWindows()
            ? Path.Combine(_dir, $"sgs-test-{Guid.NewGuid():N}.cmd")
            : Path.Combine(_dir, $"sgs-test-{Guid.NewGuid():N}.sh");
        File.WriteAllText(
            script,
            OperatingSystem.IsWindows()
                ? $"@echo %SGS_GAME_ID%;%SGS_API_URL%;%SGS_ACHIEVEMENTS_TOKEN%> \"{outputPath}\""
                : $"echo \"$SGS_GAME_ID;$SGS_API_URL;$SGS_ACHIEVEMENTS_TOKEN\" > \"{outputPath}\"",
            new UTF8Encoding(false));

        var services = new ServiceCollection();
        services.AddSingleton(bridge);
        await using var provider = services.BuildServiceProvider();
        var launcher = new ProcessGameLauncherService(provider.GetRequiredService<IServiceScopeFactory>());

        var result = await launcher.LaunchAsync(new GameLaunchRequest
        {
            GameId = gameId,
            Title = "Test",
            ExecutablePath = script
        });
        Assert.True(result.Started, result.Message);

        for (var i = 0; i < 100; i++)
        {
            if (File.Exists(outputPath))
            {
                try
                {
                    var text = File.ReadAllText(outputPath).Trim();
                    if (text.Length > 0)
                    {
                        return text;
                    }
                }
                catch (IOException)
                {
                    // the script is still writing
                }
            }

            await Task.Delay(100);
        }

        throw new TimeoutException("The launched script never wrote its environment.");
    }

    private sealed class ScriptedApi : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";

        public bool Offline { get; set; }
        public List<(string Url, string? Authorization, string? Body)> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (Offline)
            {
                throw new HttpRequestException("offline");
            }

            Requests.Add((
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SignedInAuth : IAuthSession
    {
        public bool IsAvailable => true;
        public bool IsLoggedIn => true;
        public UserProfileDto? CurrentUser => null;
        public string? LastError => null;

        public Task<bool> TrySilentLoginAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> LoginAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SignUpAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("player-token");
        public void SetCurrentUser(UserProfileDto? user)
        {
        }
    }
}
