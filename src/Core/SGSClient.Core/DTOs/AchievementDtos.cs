using SGSClient.Core.Enums;

namespace SGSClient.Core.DTOs;

/// <summary>Creator-side definition of one achievement; also the item type of the sync request.</summary>
public sealed class AchievementDefinitionDto
{
    /// <summary>
    /// API name used by game code, e.g. "ach_win_one_game". Matched case-insensitively and stored
    /// lowercase, so Steam-style "ACH_WIN_ONE_GAME" works too. Never renamed.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    /// <summary>Achieved icon: http(s) URL, or an app-relative path (<c>/api/games/media/…</c>) for an uploaded icon.</summary>
    public string? IconUrl { get; init; }

    /// <summary>Unachieved icon. null = clients show the achieved icon dimmed.</summary>
    public string? LockedIconUrl { get; init; }

    /// <summary>
    /// API name of the stat that drives this achievement. Requires <see cref="Target"/>: the stat's value
    /// is the progress and the achievement unlocks when it reaches the target.
    /// </summary>
    public string? StatKey { get; init; }

    /// <summary>Title/description per language code ("en", "de", …). The base text is the fallback.</summary>
    public IReadOnlyDictionary<string, AchievementTextDto>? Translations { get; init; }

    public int Points { get; init; }

    /// <summary>Hidden achievements show no title, description or icon until unlocked.</summary>
    public bool Hidden { get; init; }

    /// <summary>null = one-shot unlock; a number = counter that unlocks when progress reaches it.</summary>
    public int? Target { get; init; }

    public int SortOrder { get; init; }

    /// <summary>Output only. Keys left out of a sync are deactivated, never deleted.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Output only: how many players have unlocked it.</summary>
    public int UnlockedCount { get; init; }
}

public sealed class AchievementTextDto
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>PUT api/games/{id}/studio/achievements — the complete set of definitions for a game.</summary>
public sealed class SyncGameAchievementsRequest
{
    public IReadOnlyList<AchievementDefinitionDto> Items { get; init; } = [];

    /// <summary>Complete set of stat definitions; null leaves the game's stats as they are.</summary>
    public IReadOnlyList<GameStatDefinitionDto>? Stats { get; init; }
}

/// <summary>A per-player number a game stores (kills, distance…). Achievements can unlock on a threshold.</summary>
public sealed class GameStatDefinitionDto
{
    /// <summary>API name used by game code, same rules as an achievement key.</summary>
    public string Key { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
    public GameStatType Type { get; init; }

    /// <summary>null = no lower bound.</summary>
    public double? MinValue { get; init; }

    /// <summary>null = no upper bound.</summary>
    public double? MaxValue { get; init; }

    public double DefaultValue { get; init; }

    /// <summary>Largest change one call may make; null = unlimited.</summary>
    public double? MaxChange { get; init; }

    /// <summary>The value may only go up.</summary>
    public bool IncrementOnly { get; init; }

    public int SortOrder { get; init; }

    /// <summary>Output only. Keys left out of a sync are deactivated, never deleted.</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>A player's current value of one stat (the default when never set).</summary>
public sealed class GameStatValueDto
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public GameStatType Type { get; init; }
    public double Value { get; init; }
}

/// <summary>Body of POST .../stats/{key}. Absolute value, like Steam's SetStat.</summary>
public sealed class SetStatRequest
{
    public double Value { get; init; }
}

/// <summary>Answer to a stat update, with every achievement that stat moved.</summary>
public sealed class StatUpdateResultDto
{
    public string Key { get; init; } = string.Empty;
    public double Value { get; init; }
    public IReadOnlyList<AchievementUpdateResultDto> Achievements { get; init; } = [];
}

/// <summary>Player view of a game's achievements (GET api/games/{id}/achievements).</summary>
public sealed class GameAchievementsDto
{
    public int GameId { get; init; }
    public int Total { get; init; }
    public int Unlocked { get; init; }
    public int PointsTotal { get; init; }
    public int PointsEarned { get; init; }
    public IReadOnlyList<GameAchievementDto> Items { get; init; } = [];

    /// <summary>The viewer's stat values (defaults when signed out).</summary>
    public IReadOnlyList<GameStatValueDto> Stats { get; init; } = [];
}

public sealed class GameAchievementDto
{
    public string Key { get; init; } = string.Empty;

    /// <summary>Empty for a hidden achievement the viewer has not unlocked yet.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Empty for a hidden achievement the viewer has not unlocked yet.</summary>
    public string Description { get; init; } = string.Empty;

    public string? IconUrl { get; init; }

    /// <summary>Unachieved icon; null = show <see cref="IconUrl"/> dimmed. Empty for a masked hidden one.</summary>
    public string? LockedIconUrl { get; init; }

    public int Points { get; init; }
    public bool Hidden { get; init; }
    public int? Target { get; init; }

    /// <summary>Stat that drives the progress, if any.</summary>
    public string? StatKey { get; init; }

    public int Progress { get; init; }
    public DateTimeOffset? UnlockedAtUtc { get; init; }

    /// <summary>Share of the game's owners that unlocked it (0–100). Computed by the server.</summary>
    public double RarityPercent { get; init; }
}

/// <summary>Body of POST .../achievements/{key}/progress. Absolute value, so retries are safe.</summary>
public sealed class SetAchievementProgressRequest
{
    public int Value { get; init; }
}

/// <summary>Answer to unlock and progress calls.</summary>
public sealed class AchievementUpdateResultDto
{
    public string Key { get; init; } = string.Empty;
    public int Progress { get; init; }
    public int? Target { get; init; }
    public bool Unlocked { get; init; }

    /// <summary>True only for the call that flipped it from locked to unlocked.</summary>
    public bool JustUnlocked { get; init; }
}

/// <summary>Short-lived token a launched game uses to talk to api/sdk/achievements.</summary>
public sealed class AchievementSessionDto
{
    public int GameId { get; init; }
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }
}

/// <summary>One line per game on a player's profile.</summary>
public sealed class UserAchievementSummaryDto
{
    public int GameId { get; init; }
    public string GameTitle { get; init; } = string.Empty;
    public int Unlocked { get; init; }
    public int Total { get; init; }
    public int PointsEarned { get; init; }
}
