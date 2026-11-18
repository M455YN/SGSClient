namespace SGSClient.Core.Interfaces;

public interface INetworkStatusService
{
    bool IsOnline { get; }

    /// <summary>The server reports maintenance: only game downloads work (super admins keep full access).</summary>
    bool IsMaintenance { get; }

    /// <summary>The server is a development/test API (its DevelopmentBanner setting is on).</summary>
    bool IsDevelopment { get; }

    event EventHandler? Changed;

    Task StartAsync(CancellationToken cancellationToken = default);
}
