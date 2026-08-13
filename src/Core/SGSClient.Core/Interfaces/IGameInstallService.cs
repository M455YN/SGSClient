using SGSClient.Core.DTOs;
using SGSClient.Core.Models;

namespace SGSClient.Core.Interfaces;

public interface IGameInstallService
{
    /// <summary>Resolves a build download URL (handles relative ZipLink from API).</summary>
    string ResolveDownloadUrl(GameBuildDto build);

    /// <summary>Downloads and extracts a game build; updates the library item on success.</summary>
    Task<GameInstallResult> InstallAsync(
        GameLibraryItem item,
        string? version = null,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes installed files but keeps the library entry (Steam-like).</summary>
    Task<GameInstallResult> UninstallAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default);

    /// <summary>Moves installed files to another registered library folder.</summary>
    Task<GameInstallResult> MoveAsync(
        GameLibraryItem item,
        string destinationLibraryRoot,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class GameInstallResult
{
    public bool Success { get; init; }
    public GameLibraryItem? Item { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static GameInstallResult Ok(GameLibraryItem item) => new()
    {
        Success = true,
        Item = item
    };

    public static GameInstallResult Fail(params string[] errors) => new()
    {
        Success = false,
        Errors = errors
    };
}
