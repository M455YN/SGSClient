namespace SGSClient.Infrastructure.Server.Persistence.Entities;

/// <summary>Definition of one achievement, set by the game's creator.</summary>
public sealed class GameAchievementEntity
{
    public int Id { get; set; }
    public int GameId { get; set; }

    /// <summary>Stable lowercase id used by game code. Unique per game, never renamed.</summary>
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }

    /// <summary>JSON map of language code to title/description; null = no translations.</summary>
    public string? TranslationsJson { get; set; }

    public int Points { get; set; }
    public bool Hidden { get; set; }

    /// <summary>null = one-shot; a number = counter that unlocks when progress reaches it.</summary>
    public int? Target { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Removed definitions are deactivated so players keep their unlocks.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public GameEntity? Game { get; set; }
}
