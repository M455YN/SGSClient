namespace SGSClient.Core.DTOs;

/// <summary>Creator-side definition of one achievement; also the item type of the sync request.</summary>
public sealed class AchievementDefinitionDto
{
    /// <summary>Stable id used by game code, e.g. "first_boss". Lowercase, never renamed.</summary>
    public string Key { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    /// <summary>http(s) URL, or an app-relative path (<c>/api/games/media/…</c>) for an uploaded icon.</summary>
    public string? IconUrl { get; init; }

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
}

public sealed class GameAchievementDto
{
    public string Key { get; init; } = string.Empty;

    /// <summary>Empty for a hidden achievement the viewer has not unlocked yet.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Empty for a hidden achievement the viewer has not unlocked yet.</summary>
    public string Description { get; init; } = string.Empty;

    public string? IconUrl { get; init; }
    public int Points { get; init; }
    public bool Hidden { get; init; }
    public int? Target { get; init; }
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
