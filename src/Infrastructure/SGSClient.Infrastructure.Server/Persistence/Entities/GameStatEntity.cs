using SGSClient.Core.Enums;

namespace SGSClient.Infrastructure.Server.Persistence.Entities;

/// <summary>Definition of one per-player stat, set by the game's creator (Steamworks "Stats").</summary>
public sealed class GameStatEntity
{
    public int Id { get; set; }
    public int GameId { get; set; }

    /// <summary>Stable lowercase API name used by game code. Unique per game.</summary>
    public string Key { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public GameStatType Type { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double DefaultValue { get; set; }
    public double? MaxChange { get; set; }
    public bool IncrementOnly { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Removed definitions are deactivated so players keep their values.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public GameEntity? Game { get; set; }
}
