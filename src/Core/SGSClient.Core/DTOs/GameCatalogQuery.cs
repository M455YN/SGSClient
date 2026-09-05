using SGSClient.Core.Enums;

namespace SGSClient.Core.DTOs;

public sealed class GameCatalogQuery
{
    public string? Q { get; init; }
    public string? Genre { get; init; }
    public string? Tag { get; init; }
    public GameReleaseStatus? ReleaseStatus { get; init; }
    public bool? Featured { get; init; }

    /// <summary>One of: featured (default), title, newest.</summary>
    public string? Sort { get; init; }
}
