namespace SGSClient.Core.DTOs;

// Contract for the creative content API (artworks, tracks, stories). They live in their own
// tables and endpoints, separate from games. URLs below are relative to the API base address.

/// <summary>Filters shared by the art, music and stories lists.</summary>
public sealed class CreativeQuery
{
    public string? Q { get; init; }
    public string? Tag { get; init; }

    /// <summary>newest (default) | title</summary>
    public string? Sort { get; init; }
}

/// <summary>An uploaded image. GET api/art, api/art/{id}; the image itself is GET api/art/{id}/file.</summary>
public sealed class ArtworkDto
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public string? AuthorDisplayName { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>"api/art/{id}/file"</summary>
    public required string FileUrl { get; init; }

    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

/// <summary>An uploaded track. GET api/music, api/music/{id}; audio is GET api/music/{id}/file (range requests), cover GET api/music/{id}/cover.</summary>
public sealed class TrackDto
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public string? AuthorDisplayName { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>"api/music/{id}/file"</summary>
    public required string FileUrl { get; init; }

    /// <summary>"api/music/{id}/cover" or null when the track has no cover.</summary>
    public string? CoverUrl { get; init; }

    public string FileName { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class StoryChapterDto
{
    /// <summary>1-based position.</summary>
    public int Index { get; init; }

    public required string Title { get; init; }
    public required string Text { get; init; }
}

/// <summary>A story. GET api/stories (list, Chapters empty), api/stories/{id} (with chapters), cover GET api/stories/{id}/cover.</summary>
public sealed class StoryDto
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public string? AuthorDisplayName { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>"api/stories/{id}/cover" or null.</summary>
    public string? CoverUrl { get; init; }

    public int ChapterCount { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }

    /// <summary>Filled only by the single-story endpoint.</summary>
    public IReadOnlyList<StoryChapterDto> Chapters { get; init; } = [];
}

/// <summary>Body of POST api/stories (JSON). The cover is uploaded afterwards with POST api/stories/{id}/cover (multipart, field "cover").</summary>
public sealed class CreateStoryRequestDto
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public required IReadOnlyList<CreateStoryChapterDto> Chapters { get; init; }
}

public sealed class CreateStoryChapterDto
{
    public required string Title { get; init; }
    public required string Text { get; init; }
}

// ---- Editing, moderation and comments ----------------------------------------------------------
// section = "art" | "music" | "stories" (the route segment under api/).
// PUT api/{art|music}/{id} (JSON UpdateCreativeRequestDto), PUT api/stories/{id} (JSON UpdateStoryRequestDto, replaces all chapters),
// POST api/music/{id}/cover and POST api/stories/{id}/cover (multipart field "cover"): owner or admin.
// DELETE api/{section}/{id}: owner or admin. Admin lists (everything, also by other authors): GET api/admin/{art|music|stories}.
// Comments: GET api/{section}/{id}/comments (anonymous), POST api/{section}/{id}/comments (JSON AddCreativeCommentRequestDto),
// DELETE api/{section}/{id}/comments/{commentId} (comment author, item owner or admin).

public sealed class UpdateCreativeRequestDto
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed class UpdateStoryRequestDto
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public required IReadOnlyList<CreateStoryChapterDto> Chapters { get; init; }
}

public sealed class CreativeCommentDto
{
    public long Id { get; init; }
    public int ItemId { get; init; }
    public int AuthorUserId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public string? AuthorAvatarUrl { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>The signed-in user wrote it.</summary>
    public bool IsMine { get; init; }

    /// <summary>The signed-in user may delete it (author, item owner or admin).</summary>
    public bool CanDelete { get; init; }
}

public sealed class AddCreativeCommentRequestDto
{
    /// <summary>1..2000 characters.</summary>
    public required string Text { get; init; }
}

