using SGSClient.Core.Enums;

namespace SGSClient.Core.DTOs;

public sealed class GameCatalogQuery
{
    public string? Q { get; init; }
    public string? Genre { get; init; }

    /// <summary>Genre slugs; a game matches when it has ANY of them.</summary>
    public IReadOnlyList<string>? Genres { get; init; }

    /// <summary>Engine slugs; a game matches when it uses ANY of them.</summary>
    public IReadOnlyList<string>? Engines { get; init; }

    /// <summary>Only games with a build for this platform.</summary>
    public GamePlatform? Platform { get; init; }

    /// <summary>Only games that have ALL of these feature flags.</summary>
    public GameFeatures? Features { get; init; }
    public string? Tag { get; init; }

    /// <summary>Only this kind (game, app); null = everything except DLC.</summary>
    public GameContentKind? Kind { get; init; }
    public GameReleaseStatus? ReleaseStatus { get; init; }
    public bool? Featured { get; init; }

    /// <summary>One of: featured (default), title, newest.</summary>
    public string? Sort { get; init; }
}
