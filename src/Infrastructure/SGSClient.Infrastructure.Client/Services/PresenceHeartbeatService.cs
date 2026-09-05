using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

/// <summary>Sends presence heartbeats while the client is running and the user is signed in.</summary>
public sealed class PresenceHeartbeatService : IDisposable
{
    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private readonly TimeSpan _interval;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public PresenceHeartbeatService(
        ISgsApiService? api,
        IAuthSession? auth,
        TimeSpan? interval = null)
    {
        _api = api;
        _auth = auth;
        _interval = interval ?? TimeSpan.FromMinutes(2.5);
    }

    public void Start()
    {
        if (_loop is not null || _api is null || _auth is null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _loop = null;
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_auth is { IsAvailable: true, IsLoggedIn: true })
                {
                    await _api!.SendPresenceAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignore transient heartbeat failures
            }

            try
            {
                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose() => Stop();
}
