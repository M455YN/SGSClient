using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class SocialRealtimeService : IAsyncDisposable
{
    private readonly IAuthSession _auth;
    private readonly string _hubUrl;
    private readonly ILogger<SocialRealtimeService>? _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HubConnection? _connection;
    private bool _handlersWired;

    public SocialRealtimeService(
        IAuthSession auth,
        IOptions<ApiClientSettings> apiOptions,
        ILogger<SocialRealtimeService>? logger = null)
    {
        _auth = auth;
        _logger = logger;
        var baseUrl = apiOptions.Value.BaseUrl.TrimEnd('/') + "/";
        _hubUrl = new Uri(new Uri(baseUrl), "hubs/social").ToString();
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<ChatMessageDto>? MessageReceived;
    public event Action<NotificationDto>? NotificationReceived;
    public event Action? NotificationsChanged;
    public event Action<bool>? ConnectionChanged;

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        EnsureConnectedAsync(cancellationToken);

    public Task StopAsync() => DisconnectAsync();

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (!_auth.IsAvailable || !_auth.IsLoggedIn)
        {
            await DisconnectAsync().ConfigureAwait(false);
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { State: HubConnectionState.Connected })
            {
                return;
            }

            if (_connection is null)
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl(_hubUrl, options =>
                    {
                        options.AccessTokenProvider = async () =>
                            await _auth.GetAccessTokenAsync().ConfigureAwait(false);
                    })
                    .WithAutomaticReconnect()
                    .Build();

                WireHandlers(_connection);
            }

            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
                ConnectionChanged?.Invoke(true);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "SignalR social hub connect failed");
            ConnectionChanged?.Invoke(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is null)
            {
                return;
            }

            try
            {
                await _connection.StopAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }

            ConnectionChanged?.Invoke(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void WireHandlers(HubConnection connection)
    {
        if (_handlersWired)
        {
            return;
        }

        _handlersWired = true;
        connection.On<ChatMessageDto>("ReceiveMessage", msg => MessageReceived?.Invoke(msg));
        connection.On<NotificationDto>("ReceiveNotification", n => NotificationReceived?.Invoke(n));
        connection.On("NotificationsChanged", () => NotificationsChanged?.Invoke());
        connection.Reconnected += _ =>
        {
            ConnectionChanged?.Invoke(true);
            return Task.CompletedTask;
        };
        connection.Closed += _ =>
        {
            ConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };
        connection.Reconnecting += _ =>
        {
            ConnectionChanged?.Invoke(false);
            return Task.CompletedTask;
        };
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        _gate.Dispose();
    }
}
