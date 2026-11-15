namespace SGSClient.Core.Models;

public enum InstallPhase
{
    Queued,
    Downloading,
    Installing,
    Completed,
    Failed
}

public sealed class InstallProgress
{
    public InstallPhase Phase { get; init; }
    public double Fraction { get; init; }
    public string? Message { get; init; }

    /// <summary>Bytes of the package received so far; 0 outside the download phase or when the size is unknown.</summary>
    public long BytesReceived { get; init; }

    public long TotalBytes { get; init; }

    public static InstallProgress Of(
        InstallPhase phase,
        double fraction,
        string? message = null,
        long bytesReceived = 0,
        long totalBytes = 0) => new()
    {
        Phase = phase,
        Fraction = Math.Clamp(fraction, 0, 1),
        Message = message,
        BytesReceived = bytesReceived,
        TotalBytes = totalBytes
    };
}
