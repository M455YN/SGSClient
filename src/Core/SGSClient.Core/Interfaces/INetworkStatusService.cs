namespace SGSClient.Core.Interfaces;

public interface INetworkStatusService
{
    bool IsOnline { get; }

    event EventHandler? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);
}
