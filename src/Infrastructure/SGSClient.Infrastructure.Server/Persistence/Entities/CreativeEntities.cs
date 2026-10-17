namespace SGSClient.Infrastructure.Server.Persistence.Entities;

// UserId is the owner, kept as a plain column (no FK) like GameEntity.UserId, so local dev without Azure works.

/// <summary>Fields shared by the three owner-published content kinds, used by the common list filter.</summary>
public interface ICreativeEntity
{
    int Id { get; }
    int UserId { get; }
    string Title { get; set; }
    string? Description { get; set; }
    string? TagsCsv { get; set; }
    DateTimeOffset CreatedAtUtc { get; }
}

public sealed class ArtworkEntity : ICreativeEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? TagsCsv { get; set; }
    public required string StoredFileName { get; set; }
    public required string OriginalFileName { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TrackEntity : ICreativeEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? TagsCsv { get; set; }
    public required string StoredFileName { get; set; }
    public required string OriginalFileName { get; set; }
    public string? CoverFileName { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StoryEntity : ICreativeEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? TagsCsv { get; set; }
    public string? CoverFileName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<StoryChapterEntity> Chapters { get; set; } = new List<StoryChapterEntity>();
}

public sealed class StoryChapterEntity
{
    public int Id { get; set; }
    public int StoryId { get; set; }
    public StoryEntity? Story { get; set; }

    /// <summary>1-based position.</summary>
    public int Index { get; set; }

    public required string Title { get; set; }
    public required string Text { get; set; }
}

/// <summary>Comment on an art/music/stories item. Shared table, so no FK: services delete comments with the item.</summary>
public sealed class CreativeCommentEntity
{
    public long Id { get; set; }
    public required string Section { get; set; }
    public int ItemId { get; set; }
    public int AuthorUserId { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
