namespace SGSClient.Core.Interfaces;

public interface INetworkStatusService
{
    bool IsOnline { get; }

    /// <summary>The server reports maintenance: only game downloads work (super admins keep full access).</summary>
    bool IsMaintenance { get; }

    event EventHandler? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);
}
