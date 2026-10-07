using SGSClient.Core.DTOs;
using SGSClient.Core.Packaging;

namespace SGSClient.Core.Interfaces;

public interface IGamePackagePublisher
{
    /// <param name="allowOverwriteForeignGame">
    /// When true (superadmin), an existing game owned by someone else may be republished.
    /// Ownership of the game is not transferred.
    /// </param>
    Task<GamePackagePublishResult> PublishZipAsync(
        Stream zipStream,
        long contentLength,
        int ownerUserId,
        bool allowOverwriteForeignGame = false,
        CancellationToken cancellationToken = default);
}

public sealed class GamePackagePublishResult
{
    public bool Success { get; init; }
    public GameDto? Game { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static GamePackagePublishResult Ok(GameDto game) => new()
    {
        Success = true,
        Game = game
    };

    public static GamePackagePublishResult Fail(params string[] errors) => new()
    {
        Success = false,
        Errors = errors
    };

    public static GamePackagePublishResult Fail(IEnumerable<string> errors) => new()
    {
        Success = false,
        Errors = errors.ToArray()
    };
}

public interface IGamePackagePacker
{
    /// <summary>Reads <c>sgs-manifest.json</c> from a game folder without packing.</summary>
    Task<GamePackageManifestLoadResult> LoadManifestAsync(
        string folderPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Packs a folder into a ZIP. When <paramref name="manifestOverride"/> is set,
    /// it is validated, written back to the folder, and used as the ZIP root manifest.
    /// </summary>
    Task<GamePackagePackResult> PackFolderAsync(
        string folderPath,
        SgsPackageManifest? manifestOverride = null,
        CancellationToken cancellationToken = default);
}

public sealed class GamePackageManifestLoadResult
{
    public bool Success { get; init; }
    public SgsPackageManifest? Manifest { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static GamePackageManifestLoadResult Ok(SgsPackageManifest manifest) => new()
    {
        Success = true,
        Manifest = manifest
    };

    public static GamePackageManifestLoadResult Fail(params string[] errors) => new()
    {
        Success = false,
        Errors = errors
    };
}

public sealed class GamePackagePackResult
{
    public bool Success { get; init; }
    public string? ZipPath { get; init; }
    public SgsPackageManifest? Manifest { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static GamePackagePackResult Ok(string zipPath, SgsPackageManifest manifest) => new()
    {
        Success = true,
        ZipPath = zipPath,
        Manifest = manifest
    };

    public static GamePackagePackResult Fail(params string[] errors) => new()
    {
        Success = false,
        Errors = errors
    };
}
