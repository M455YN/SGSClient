namespace SGSClient.Core.DTOs;

/// <summary>Download statistics for one game, shown in the creator dashboard.</summary>
public sealed class GameStatsDto
{
    public int GameId { get; init; }
    public int TotalDownloads { get; init; }
    public int DownloadsLast30Days { get; init; }
    public int DownloadsLast7Days { get; init; }
    public DateTimeOffset? LastDownloadUtc { get; init; }

    /// <summary>One entry per day for the last 30 days, oldest first, including days with zero.</summary>
    public IReadOnlyList<GameStatsPointDto> Daily { get; init; } = [];

    /// <summary>Downloads grouped by version, most downloaded first.</summary>
    public IReadOnlyList<GameStatsVersionDto> ByVersion { get; init; } = [];
}

public sealed class GameStatsPointDto
{
    public DateOnly Day { get; init; }
    public int Downloads { get; init; }
}

public sealed class GameStatsVersionDto
{
    public required string Version { get; init; }
    public int Downloads { get; init; }
}
