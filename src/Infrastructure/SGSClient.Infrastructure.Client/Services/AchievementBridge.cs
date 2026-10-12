using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SGSClient.Core.Achievements;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

/// <summary>
/// A launched game talks to this loopback endpoint instead of the API: same paths and header as
/// <c>api/sdk/achievements</c>, but it signs calls with the player's own sign-in, so there is no token
/// to expire, and when the API cannot be reached it queues unlock/progress calls on disk and replays
/// them later. Whatever the API definitively answers (200, 400, 403, 404) is relayed unchanged.
/// </summary>
public sealed class AchievementBridge : IDisposable
{
    public const string TokenHeader = "X-SGS-Token";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IAuthSession _auth;
    private readonly string _apiBase;
    private readonly HttpClient _http;
    private readonly string _queuePath;
    private readonly TimeSpan _flushInterval;
    private readonly object _gate = new();
    private readonly object _startLock = new();
    // shortcut: session tokens live as long as the client process, drop them on game exit if that ever matters.
    private readonly ConcurrentDictionary<string, int> _sessions = new();
    private List<QueuedCall> _queue = [];
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private string? _baseUrl;
    private int _flushing;

    public AchievementBridge(
        IAuthSession auth,
        IOptions<ApiClientSettings> api,
        HttpMessageHandler? handler = null,
        string? queuePath = null,
        TimeSpan? flushInterval = null)
    {
        _auth = auth;
        _apiBase = api.Value.BaseUrl.TrimEnd('/') + "/";
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(10);
        _queuePath = queuePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SGSClient",
            "achievement-queue.json");
        _flushInterval = flushInterval ?? TimeSpan.FromMinutes(1);
        _queue = LoadQueue();
    }

    /// <summary>Calls waiting for the API to become reachable.</summary>
    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>Starts the loopback listener and the replay loop. Safe to call repeatedly.</summary>
    public string? Start()
    {
        lock (_startLock)
        {
            return StartCore();
        }
    }

    private string? StartCore()
    {
        if (_baseUrl is not null)
        {
            return _baseUrl;
        }

        if (!HttpListener.IsSupported)
        {
            return null;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var port = FreeLoopbackPort();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
            }
            catch (HttpListenerException)
            {
                listener.Close();
                continue;
            }

            _listener = listener;
            _cts = new CancellationTokenSource();
            _baseUrl = $"http://127.0.0.1:{port}";
            _ = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
            _ = Task.Run(() => FlushLoopAsync(_cts.Token));
            return _baseUrl;
        }

        return null;
    }

    /// <summary>The environment variables for one launch of <paramref name="gameId"/>; null when no bridge.</summary>
    public IReadOnlyDictionary<string, string>? CreateSession(int gameId)
    {
        var baseUrl = Start();
        if (baseUrl is null)
        {
            return null;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _sessions[token] = gameId;
        return new Dictionary<string, string>
        {
            ["SGS_API_URL"] = baseUrl,
            ["SGS_GAME_ID"] = gameId.ToString(CultureInfo.InvariantCulture),
            ["SGS_ACHIEVEMENTS_TOKEN"] = token
        };
    }

    /// <summary>Replays queued calls for the signed-in player until one cannot be delivered.</summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _flushing, 1) == 1)
        {
            return;
        }

        try
        {
            while (true)
            {
                QueuedCall? next;
                lock (_gate)
                {
                    var current = _auth.CurrentUser?.Id;
                    next = _queue.FirstOrDefault(c => c.UserId is null || current is null || c.UserId == current);
                }

                if (next is null)
                {
                    return;
                }

                var (status, _) = await SendAsync(next, cancellationToken).ConfigureAwait(false);
                if (status is not { } code || !IsDefinitive(code))
                {
                    return;
                }

                lock (_gate)
                {
                    _queue.Remove(next);
                    SaveQueue();
                }
            }
        }
        finally
        {
            Volatile.Write(ref _flushing, 0);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try
        {
            _listener?.Close();
        }
        catch
        {
            // already closed
        }

        _http.Dispose();
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch
            {
                return; // listener closed
            }

            _ = Task.Run(() => HandleAsync(context, cancellationToken), cancellationToken);
        }
    }

    private async Task FlushLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_flushInterval);
            do
            {
                if (PendingCount > 0)
                {
                    await FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // client is closing
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            // A WebGL build calls from a page origin, so the bridge answers CORS pre-flights.
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Headers"] = $"{TokenHeader}, Content-Type";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";

            var request = context.Request;
            if (request.HttpMethod == "OPTIONS")
            {
                await RespondAsync(context, HttpStatusCode.NoContent, null);
                return;
            }

            if (!_sessions.TryGetValue(request.Headers[TokenHeader] ?? string.Empty, out var gameId))
            {
                await RespondAsync(context, HttpStatusCode.Unauthorized, Error("Brak lub nieprawidłowy token gry."));
                return;
            }

            var segments = request.Url!.AbsolutePath.Trim('/').Split('/');
            if (segments is not ["api", "sdk", "achievements", ..])
            {
                await RespondAsync(context, HttpStatusCode.NotFound, Error("Nieznany adres."));
                return;
            }

            if (segments.Length == 3 && request.HttpMethod == "GET")
            {
                await RelayListAsync(context, gameId, cancellationToken);
                return;
            }

            if (segments.Length == 5
                && request.HttpMethod == "POST"
                && segments[4] is "unlock" or "progress"
                && AchievementRules.NormalizeKey(segments[3]) is { } key)
            {
                await RelayCallAsync(context, gameId, key, segments[4] == "progress", cancellationToken);
                return;
            }

            await RespondAsync(context, HttpStatusCode.NotFound, Error("Nieznany adres."));
        }
        catch
        {
            try
            {
                await RespondAsync(context, HttpStatusCode.InternalServerError, Error("Błąd mostu osiągnięć."));
            }
            catch
            {
                // the game hung up
            }
        }
    }

    private async Task RelayCallAsync(
        HttpListenerContext context,
        int gameId,
        string key,
        bool isProgress,
        CancellationToken cancellationToken)
    {
        int? value = null;
        if (isProgress)
        {
            if (context.Request.ContentLength64 > 1024)
            {
                await RespondAsync(context, HttpStatusCode.RequestEntityTooLarge, Error("Treść jest za duża."));
                return;
            }

            try
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var body = JsonSerializer.Deserialize<SetAchievementProgressRequest>(
                    await reader.ReadToEndAsync(cancellationToken), Web);
                value = body?.Value;
            }
            catch (JsonException)
            {
            }

            if (value is null or < 0)
            {
                await RespondAsync(context, HttpStatusCode.BadRequest, Error("Podaj nieujemną wartość: {\"value\": 5}."));
                return;
            }
        }

        var call = new QueuedCall(_auth.CurrentUser?.Id, gameId, key, value);
        var (status, responseBody) = await SendAsync(call, cancellationToken);
        if (status is { } code && IsDefinitive(code))
        {
            await RespondAsync(context, code, responseBody);
            if ((int)code < 300)
            {
                _ = FlushAsync(cancellationToken);
            }

            return;
        }

        Enqueue(call);
        await RespondAsync(
            context,
            HttpStatusCode.Accepted,
            JsonSerializer.Serialize(
                new { key, progress = value ?? 0, target = (int?)null, unlocked = false, justUnlocked = false, queued = true },
                Web));
    }

    private async Task RelayListAsync(HttpListenerContext context, int gameId, CancellationToken cancellationToken)
    {
        var token = await TryGetBearerAsync(cancellationToken);
        if (token is null)
        {
            await RespondAsync(context, HttpStatusCode.ServiceUnavailable, Error("Zaloguj się w kliencie SGS."));
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_apiBase}api/games/{gameId}/achievements{context.Request.Url!.Query}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await _http.SendAsync(request, cancellationToken);
            await RespondAsync(
                context,
                response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (HttpRequestException)
        {
            await RespondAsync(context, HttpStatusCode.ServiceUnavailable, Error("Brak połączenia z serwerem SGS."));
        }
        catch (TaskCanceledException)
        {
            await RespondAsync(context, HttpStatusCode.ServiceUnavailable, Error("Serwer SGS nie odpowiada."));
        }
    }

    /// <summary>Status is null when the call could not be delivered (offline, signed out, timeout).</summary>
    private async Task<(HttpStatusCode? Status, string? Body)> SendAsync(QueuedCall call, CancellationToken cancellationToken)
    {
        var token = await TryGetBearerAsync(cancellationToken);
        if (token is null)
        {
            return (null, null);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_apiBase}api/me/games/{call.GameId}/achievements/{call.Key}/{(call.Value is null ? "unlock" : "progress")}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (call.Value is int value)
            {
                request.Content = JsonContent.Create(new SetAchievementProgressRequest { Value = value });
            }

            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (null, null);
        }
    }

    private async Task<string?> TryGetBearerAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await _auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 4xx is the API's final word (bad key, game not owned); 401 and 5xx are worth retrying later.
    /// </summary>
    private static bool IsDefinitive(HttpStatusCode status) =>
        (int)status < 500
        && status is not (HttpStatusCode.Unauthorized or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests);

    private void Enqueue(QueuedCall call)
    {
        lock (_gate)
        {
            var index = _queue.FindIndex(c =>
                c.UserId == call.UserId && c.GameId == call.GameId && c.Key == call.Key
                && (c.Value is null) == (call.Value is null));
            if (index < 0)
            {
                _queue.Add(call);
            }
            else if (call.Value is int value)
            {
                _queue[index] = _queue[index] with { Value = Math.Max(_queue[index].Value ?? 0, value) };
            }

            SaveQueue();
        }
    }

    private List<QueuedCall> LoadQueue()
    {
        try
        {
            return File.Exists(_queuePath)
                ? JsonSerializer.Deserialize<List<QueuedCall>>(File.ReadAllText(_queuePath), Web) ?? []
                : [];
        }
        catch
        {
            return []; // a damaged queue file must never stop the client
        }
    }

    private void SaveQueue()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_queuePath)!);
            File.WriteAllText(_queuePath, JsonSerializer.Serialize(_queue, Web));
        }
        catch
        {
            // keep playing; the in-memory queue still works until the client closes
        }
    }

    private static int FreeLoopbackPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        try
        {
            return ((IPEndPoint)probe.LocalEndpoint).Port;
        }
        finally
        {
            probe.Stop();
        }
    }

    private static string Error(string message) => JsonSerializer.Serialize(new { errors = new[] { message } }, Web);

    private static async Task RespondAsync(HttpListenerContext context, HttpStatusCode status, string? body)
    {
        context.Response.StatusCode = (int)status;
        if (!string.IsNullOrEmpty(body))
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }

        context.Response.Close();
    }

    private sealed record QueuedCall(int? UserId, int GameId, string Key, int? Value);
}
