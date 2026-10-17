using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public enum CreativeDeleteResult
{
    Deleted,
    NotFound,
    Forbidden
}

// Validation failures are reported as ArgumentException (message shown to the user), like IGameMediaService.

public interface IArtworkService
{
    Task<IReadOnlyList<ArtworkDto>> ListAsync(CreativeQuery? query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArtworkDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default);
    Task<ArtworkDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ArtworkDto> CreateAsync(
        int userId,
        string title,
        string? description,
        IReadOnlyList<string> tags,
        Stream file,
        string originalFileName,
        long sizeBytes,
        CancellationToken cancellationToken = default);

    Task<CreativeDeleteResult> DeleteAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken = default);

    /// <summary>Everything, all authors, newest first (admin lists).</summary>
    Task<IReadOnlyList<ArtworkDto>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Owner or admin. Null when missing; <see cref="UnauthorizedAccessException"/> otherwise.</summary>
    Task<ArtworkDto?> UpdateAsync(int id, int userId, bool isAdmin, UpdateCreativeRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Absolute path of the stored image or null.</summary>
    Task<string?> GetFilePathAsync(int id, CancellationToken cancellationToken = default);
}

public interface IMusicService
{
    Task<IReadOnlyList<TrackDto>> ListAsync(CreativeQuery? query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default);
    Task<TrackDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<TrackDto> CreateAsync(
        int userId,
        string title,
        string? description,
        IReadOnlyList<string> tags,
        Stream file,
        string originalFileName,
        long sizeBytes,
        Stream? cover,
        string? coverFileName,
        long coverSizeBytes,
        CancellationToken cancellationToken = default);

    Task<CreativeDeleteResult> DeleteAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrackDto>> ListAllAsync(CancellationToken cancellationToken = default);
    Task<TrackDto?> UpdateAsync(int id, int userId, bool isAdmin, UpdateCreativeRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Owner or admin; replaces the old cover file.</summary>
    Task<TrackDto?> SetCoverAsync(int id, int userId, bool isAdmin, Stream cover, string coverFileName, long coverSizeBytes, CancellationToken cancellationToken = default);

    Task<string?> GetFilePathAsync(int id, CancellationToken cancellationToken = default);
    Task<string?> GetCoverPathAsync(int id, CancellationToken cancellationToken = default);
}

public interface IStoryService
{
    Task<IReadOnlyList<StoryDto>> ListAsync(CreativeQuery? query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoryDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>The story with its chapters.</summary>
    Task<StoryDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<StoryDto> CreateAsync(int userId, CreateStoryRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Owner or admin. Null when the story does not exist; <see cref="UnauthorizedAccessException"/> otherwise.</summary>
    Task<StoryDto?> SetCoverAsync(
        int id,
        int userId,
        bool isAdmin,
        Stream cover,
        string coverFileName,
        long coverSizeBytes,
        CancellationToken cancellationToken = default);

    Task<CreativeDeleteResult> DeleteAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoryDto>> ListAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Owner or admin; replaces all chapters.</summary>
    Task<StoryDto?> UpdateAsync(int id, int userId, bool isAdmin, UpdateStoryRequestDto request, CancellationToken cancellationToken = default);

    Task<string?> GetCoverPathAsync(int id, CancellationToken cancellationToken = default);
}

public static class CreativeSections
{
    public const string Art = "art";
    public const string Music = "music";
    public const string Stories = "stories";
}

public interface ICreativeCommentService
{
    /// <summary>Oldest first. Null when the item does not exist. <paramref name="viewerUserId"/> null = anonymous.</summary>
    Task<IReadOnlyList<CreativeCommentDto>?> ListAsync(string section, int itemId, int? viewerUserId, bool viewerIsAdmin, CancellationToken cancellationToken = default);

    /// <summary>Null when the item does not exist; ArgumentException on invalid text.</summary>
    Task<CreativeCommentDto?> AddAsync(string section, int itemId, int userId, bool isAdmin, string? text, CancellationToken cancellationToken = default);

    Task<CreativeDeleteResult> DeleteAsync(string section, int itemId, long commentId, int userId, bool isAdmin, CancellationToken cancellationToken = default);
}
