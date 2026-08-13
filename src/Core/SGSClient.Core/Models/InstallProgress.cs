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

    public static InstallProgress Of(InstallPhase phase, double fraction, string? message = null) => new()
    {
        Phase = phase,
        Fraction = Math.Clamp(fraction, 0, 1),
        Message = message
    };
}
