namespace SGSClient.Core.DTOs;

public sealed class PlayerTagAvailabilityDto
{
    public bool Available { get; init; }
    public string? Normalized { get; init; }
    public string? Error { get; init; }
}
