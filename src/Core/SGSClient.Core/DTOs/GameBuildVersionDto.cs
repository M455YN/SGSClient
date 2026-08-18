namespace SGSClient.Core.DTOs;

public sealed class GameBuildVersionDto
{
    public int Id { get; init; }
    public int GameBuildId { get; init; }
    public required string Version { get; init; }
    public required string ExeName { get; init; }
    public required string ZipLink { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }
    public bool IsCurrent { get; init; }

    /// <summary>Creator changelog for this version.</summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>Platform label of the parent build, e.g. "windows x64".</summary>
    public string? PlatformLabel { get; init; }
}

/// <summary>Owner edit of a published version's changelog.</summary>
public sealed class UpdateVersionNotesRequestDto
{
    public string? ReleaseNotes { get; init; }
}
