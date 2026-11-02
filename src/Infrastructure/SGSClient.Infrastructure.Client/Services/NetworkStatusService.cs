using System.Net.Http.Json;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class NetworkStatusService : INetworkStatusService, IDisposable
{
    private readonly IOptions<ApiClientSettings> _apiOptions;
    private readonly HttpClient _http;
    private readonly object _gate = new();
    private bool _isOnline = true;
    private bool _isMaintenance;
    private CancellationTokenSource? _loopCts;

    public NetworkStatusService(IOptions<ApiClientSettings> apiOptions, IHttpClientFactory? httpFactory = null)
    {
        _apiOptions = apiOptions;
        _http = httpFactory?.CreateClient(nameof(NetworkStatusService))
            ?? new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    }

    public bool IsOnline
    {
        get
        {
            lock (_gate)
            {
                return _isOnline;
            }
        }
    }

    public bool IsMaintenance
    {
        get
        {
            lock (_gate)
            {
                return _isMaintenance;
            }
        }
    }

    public event EventHandler? Changed;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => ProbeLoopAsync(_loopCts.Token));
        return ProbeOnceAsync(cancellationToken);
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _http.Dispose();
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _ = ProbeOnceAsync(CancellationToken.None);
    }

    private async Task ProbeLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken).ConfigureAwait(false);
                await ProbeOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // keep probing
            }
        }
    }

    private async Task ProbeOnceAsync(CancellationToken cancellationToken)
    {
        var online = NetworkInterface.GetIsNetworkAvailable();
        var maintenance = false;
        if (online)
        {
            try
            {
                var baseUrl = _apiOptions.Value.BaseUrl?.TrimEnd('/') ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/Games?pageSize=1");
                    using var response = await _http.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken)
                        .ConfigureAwait(false);
                    online = response.IsSuccessStatusCode
                        || (int)response.StatusCode is >= 400 and < 500;
                    maintenance = online && await ReadMaintenanceAsync(baseUrl, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                online = false;
            }
        }

        SetStatus(online, maintenance);
    }

    private async Task<bool> ReadMaintenanceAsync(string baseUrl, CancellationToken cancellationToken)
    {
        try
        {
            var health = await _http.GetFromJsonAsync<HealthResponse>($"{baseUrl}/health", cancellationToken)
                .ConfigureAwait(false);
            return health?.Maintenance == true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private void SetStatus(bool online, bool maintenance)
    {
        bool changed;
        lock (_gate)
        {
            changed = _isOnline != online || _isMaintenance != maintenance;
            _isOnline = online;
            _isMaintenance = maintenance;
        }

        // Raised on every probe during maintenance so the banner picks up a sign-in made after it appeared.
        if (changed || maintenance)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed record HealthResponse(bool Maintenance);
}
