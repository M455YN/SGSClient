using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

/// <summary>Files live under {StoragePath}/_creative/{kind}/{id}/. Stored names are generated, never taken from uploads.</summary>
public sealed partial class CreativeStorage(IOptions<GameStorageSettings> options)
{
    [GeneratedRegex(@"^[0-9a-f]{32}\.[a-z0-9]{1,5}$")]
    private static partial Regex StoredNameRegex();

    private string Root => Path.GetFullPath(Path.Combine(options.Value.StoragePath, "_creative"));

    private string Dir(string kind, int id) =>
        Path.Combine(Root, kind, id.ToString(CultureInfo.InvariantCulture));

    public static string NewStoredName(string extension) => Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();

    public string? PathFor(string kind, int id, string? storedName)
    {
        if (storedName is null || !StoredNameRegex().IsMatch(storedName))
        {
            return null;
        }

        var dir = Path.GetFullPath(Dir(kind, id)) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(dir, storedName));
        return full.StartsWith(dir, StringComparison.Ordinal) && File.Exists(full) ? full : null;
    }

    public async Task SaveAsync(string kind, int id, string storedName, Stream content, CancellationToken cancellationToken)
    {
        if (!StoredNameRegex().IsMatch(storedName))
        {
            throw new ArgumentException("Nieprawidłowa nazwa pliku.");
        }

        Directory.CreateDirectory(Dir(kind, id));
        await using var target = File.Create(Path.Combine(Dir(kind, id), storedName));
        await content.CopyToAsync(target, cancellationToken);
    }

    public void Delete(string kind, int id, string? storedName)
    {
        var path = PathFor(kind, id, storedName);
        if (path is not null)
        {
            File.Delete(path);
        }
    }

    public void DeleteAll(string kind, int id)
    {
        var dir = Dir(kind, id);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

internal static class CreativeSupport
{
    public static string Title(string? title)
    {
        var t = title?.Trim();
        if (string.IsNullOrEmpty(t))
        {
            throw new ArgumentException("Podaj tytuł.");
        }

        return t.Length > CreativeRules.MaxTitle
            ? throw new ArgumentException($"Tytuł może mieć maks. {CreativeRules.MaxTitle} znaków.")
            : t;
    }

    public static string? Description(string? description)
    {
        var d = description?.Trim();
        if (string.IsNullOrEmpty(d))
        {
            return null;
        }

        return d.Length > CreativeRules.MaxDescription
            ? throw new ArgumentException($"Opis może mieć maks. {CreativeRules.MaxDescription} znaków.")
            : d;
    }

    /// <summary>Lowercase, trimmed, distinct; null when empty.</summary>
    public static string? Tags(IEnumerable<string>? tags)
    {
        var list = (tags ?? [])
            .Select(t => (t ?? string.Empty).Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();
        if (list.Count > CreativeRules.MaxTags)
        {
            throw new ArgumentException($"Maksymalnie {CreativeRules.MaxTags} tagów.");
        }

        if (list.Any(t => t.Length > CreativeRules.MaxTagLength))
        {
            throw new ArgumentException($"Tag może mieć maks. {CreativeRules.MaxTagLength} znaków.");
        }

        return CsvList.Join(list);
    }

    /// <summary>Returns the lower-case extension, or throws.</summary>
    public static string CheckFile(string? fileName, long size, string[] allowed, long maxBytes, string label)
    {
        if (size <= 0)
        {
            throw new ArgumentException($"Dodaj plik: {label}.");
        }

        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!allowed.Contains(ext))
        {
            throw new ArgumentException($"{label}: dozwolone rozszerzenia to {string.Join(", ", allowed)}.");
        }

        return size > maxBytes
            ? throw new ArgumentException($"{label}: maksymalny rozmiar to {maxBytes / (1024 * 1024)} MB.")
            : ext;
    }

    /// <summary>Original name kept as metadata only: file name part, truncated.</summary>
    public static string OriginalName(string? fileName)
    {
        var n = Path.GetFileName(fileName ?? string.Empty);
        return n.Length > 260 ? n[..260] : n;
    }

    public static async Task<IQueryable<T>> FilterAsync<T>(
        SgsServerDbContext db,
        IQueryable<T> source,
        CreativeQuery? query,
        CancellationToken cancellationToken) where T : class, ICreativeEntity
    {
        var tag = query?.Tag?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(tag))
        {
            var needle = "," + tag + ",";
            source = source.Where(x => ("," + x.TagsCsv + ",").Contains(needle));
        }

        var term = query?.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
        {
            var authorIds = await db.Users.AsNoTracking()
                .Where(u => u.DisplayName.ToLower().Contains(term))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
            source = source.Where(x =>
                x.Title.ToLower().Contains(term)
                || (x.Description != null && x.Description.ToLower().Contains(term))
                || (x.TagsCsv != null && x.TagsCsv.ToLower().Contains(term))
                || authorIds.Contains(x.UserId));
        }

        return string.Equals(query?.Sort, "title", StringComparison.OrdinalIgnoreCase)
            ? source.OrderBy(x => x.Title).ThenByDescending(x => EF.Property<int>(x, "Id"))
            : source.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => EF.Property<int>(x, "Id"));
    }

    /// <summary>Queues removal of the item's comments (shared table, no FK); the caller saves.</summary>
    public static async Task RemoveCommentsAsync(SgsServerDbContext db, string section, int itemId, CancellationToken cancellationToken) =>
        db.CreativeComments.RemoveRange(await db.CreativeComments.Where(c => c.Section == section && c.ItemId == itemId).ToListAsync(cancellationToken));

    public static void CheckOwner(int ownerId, int userId, bool isAdmin)
    {
        if (ownerId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("Możesz edytować tylko własne treści.");
        }
    }

    public static List<StoryChapterEntity> BuildChapters(IReadOnlyList<CreateStoryChapterDto>? chapters)
    {
        chapters ??= [];
        if (chapters.Count is < 1 or > CreativeRules.MaxChapters)
        {
            throw new ArgumentException($"Opowiadanie musi mieć od 1 do {CreativeRules.MaxChapters} rozdziałów.");
        }

        var result = new List<StoryChapterEntity>();
        var index = 0;
        foreach (var c in chapters)
        {
            index++;
            if (string.IsNullOrWhiteSpace(c.Text))
            {
                throw new ArgumentException($"Rozdział {index} nie ma treści.");
            }

            if (c.Text.Length > CreativeRules.MaxChapterText)
            {
                throw new ArgumentException($"Rozdział {index} jest za długi (maks. {CreativeRules.MaxChapterText} znaków).");
            }

            var chapterTitle = c.Title?.Trim();
            if (string.IsNullOrEmpty(chapterTitle))
            {
                chapterTitle = $"Rozdział {index}";
            }

            if (chapterTitle.Length > CreativeRules.MaxChapterTitle)
            {
                throw new ArgumentException($"Tytuł rozdziału {index} jest za długi (maks. {CreativeRules.MaxChapterTitle} znaków).");
            }

            result.Add(new StoryChapterEntity { Index = index, Title = chapterTitle, Text = c.Text });
        }

        return result;
    }

    public static async Task<Dictionary<int, string>> AuthorsAsync(
        SgsServerDbContext db,
        IEnumerable<int> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);
    }
}

public sealed class EfArtworkService(SgsServerDbContext db, CreativeStorage storage) : IArtworkService
{
    private const string Kind = "art";

    public async Task<IReadOnlyList<ArtworkDto>> ListAsync(CreativeQuery? query, CancellationToken cancellationToken = default) =>
        await ToDtosAsync(await CreativeSupport.FilterAsync(db, db.Artworks.AsNoTracking(), query, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<ArtworkDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default) =>
        await ToDtosAsync(
            await CreativeSupport.FilterAsync(db, db.Artworks.AsNoTracking().Where(x => x.UserId == userId), null, cancellationToken),
            cancellationToken);

    public async Task<ArtworkDto?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        (await ToDtosAsync(db.Artworks.AsNoTracking().Where(x => x.Id == id), cancellationToken)).FirstOrDefault();

    public async Task<ArtworkDto> CreateAsync(
        int userId, string title, string? description, IReadOnlyList<string> tags,
        Stream file, string originalFileName, long sizeBytes, CancellationToken cancellationToken = default)
    {
        var ext = CreativeSupport.CheckFile(originalFileName, sizeBytes, CreativeRules.ArtExtensions, CreativeRules.MaxArtBytes, "Obraz");
        var entity = new ArtworkEntity
        {
            UserId = userId,
            Title = CreativeSupport.Title(title),
            Description = CreativeSupport.Description(description),
            TagsCsv = CreativeSupport.Tags(tags),
            StoredFileName = CreativeStorage.NewStoredName(ext),
            OriginalFileName = CreativeSupport.OriginalName(originalFileName),
            SizeBytes = sizeBytes,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Artworks.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await storage.SaveAsync(Kind, entity.Id, entity.StoredFileName, file, cancellationToken);
        }
        catch
        {
            db.Artworks.Remove(entity);
            await db.SaveChangesAsync(CancellationToken.None);
            storage.DeleteAll(Kind, entity.Id);
            throw;
        }

        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    public async Task<CreativeDeleteResult> DeleteAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var entity = await db.Artworks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return CreativeDeleteResult.NotFound;
        }

        if (entity.UserId != userId && !isAdmin)
        {
            return CreativeDeleteResult.Forbidden;
        }

        db.Artworks.Remove(entity);
        await CreativeSupport.RemoveCommentsAsync(db, Kind, id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        storage.DeleteAll(Kind, id);
        return CreativeDeleteResult.Deleted;
    }

    public async Task<IReadOnlyList<ArtworkDto>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await ToDtosAsync(db.Artworks.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id), cancellationToken);

    public async Task<ArtworkDto?> UpdateAsync(
        int id, int userId, bool isAdmin, UpdateCreativeRequestDto request, CancellationToken cancellationToken = default)
    {
        var entity = await db.Artworks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        CreativeSupport.CheckOwner(entity.UserId, userId, isAdmin);
        entity.Title = CreativeSupport.Title(request.Title);
        entity.Description = CreativeSupport.Description(request.Description);
        entity.TagsCsv = CreativeSupport.Tags(request.Tags);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<string?> GetFilePathAsync(int id, CancellationToken cancellationToken = default)
    {
        var name = await db.Artworks.AsNoTracking().Where(x => x.Id == id).Select(x => x.StoredFileName).FirstOrDefaultAsync(cancellationToken);
        return storage.PathFor(Kind, id, name);
    }

    private async Task<IReadOnlyList<ArtworkDto>> ToDtosAsync(IQueryable<ArtworkEntity> query, CancellationToken cancellationToken)
    {
        var rows = await query.ToListAsync(cancellationToken);
        var authors = await CreativeSupport.AuthorsAsync(db, rows.Select(r => r.UserId), cancellationToken);
        return rows.Select(r => new ArtworkDto
        {
            Id = r.Id,
            UserId = r.UserId,
            AuthorDisplayName = authors.GetValueOrDefault(r.UserId),
            Title = r.Title,
            Description = r.Description,
            Tags = CsvList.Split(r.TagsCsv),
            FileUrl = $"api/art/{r.Id}/file",
            FileName = r.OriginalFileName,
            SizeBytes = r.SizeBytes,
            CreatedAtUtc = r.CreatedAtUtc
        }).ToList();
    }
}

public sealed class EfMusicService(SgsServerDbContext db, CreativeStorage storage) : IMusicService
{
    private const string Kind = "music";

    public async Task<IReadOnlyList<TrackDto>> ListAsync(CreativeQuery? query, CancellationToken cancellationToken = default) =>
        await ToDtosAsync(await CreativeSupport.FilterAsync(db, db.Tracks.AsNoTracking(), query, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<TrackDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default) =>
        await ToDtosAsync(
            await CreativeSupport.FilterAsync(db, db.Tracks.AsNoTracking().Where(x => x.UserId == userId), null, cancellationToken),
            cancellationToken);

    public async Task<TrackDto?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        (await ToDtosAsync(db.Tracks.AsNoTracking().Where(x => x.Id == id), cancellationToken)).FirstOrDefault();

    public async Task<TrackDto> CreateAsync(
        int userId, string title, string? description, IReadOnlyList<string> tags,
        Stream file, string originalFileName, long sizeBytes,
        Stream? cover, string? coverFileName, long coverSizeBytes, CancellationToken cancellationToken = default)
    {
        var ext = CreativeSupport.CheckFile(originalFileName, sizeBytes, CreativeRules.MusicExtensions, CreativeRules.MaxMusicBytes, "Utwór");
        var coverExt = cover is null
            ? null
            : CreativeSupport.CheckFile(coverFileName, coverSizeBytes, CreativeRules.CoverExtensions, CreativeRules.MaxCoverBytes, "Okładka");
        var entity = new TrackEntity
        {
            UserId = userId,
            Title = CreativeSupport.Title(title),
            Description = CreativeSupport.Description(description),
            TagsCsv = CreativeSupport.Tags(tags),
            StoredFileName = CreativeStorage.NewStoredName(ext),
            OriginalFileName = CreativeSupport.OriginalName(originalFileName),
            CoverFileName = coverExt is null ? null : CreativeStorage.NewStoredName(coverExt),
            SizeBytes = sizeBytes,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.Tracks.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await storage.SaveAsync(Kind, entity.Id, entity.StoredFileName, file, cancellationToken);
            if (cover is not null && entity.CoverFileName is not null)
            {
                await storage.SaveAsync(Kind, entity.Id, entity.CoverFileName, cover, cancellationToken);
            }
        }
        catch
        {
            db.Tracks.Remove(entity);
            await db.SaveChangesAsync(CancellationToken.None);
            storage.DeleteAll(Kind, entity.Id);
            throw;
        }

        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    public async Task<CreativeDeleteResult> DeleteAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var entity = await db.Tracks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return CreativeDeleteResult.NotFound;
        }

        if (entity.UserId != userId && !isAdmin)
        {
            return CreativeDeleteResult.Forbidden;
        }

        db.Tracks.Remove(entity);
        await CreativeSupport.RemoveCommentsAsync(db, Kind, id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        storage.DeleteAll(Kind, id);
        return CreativeDeleteResult.Deleted;
    }

    public async Task<IReadOnlyList<TrackDto>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await ToDtosAsync(db.Tracks.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id), cancellationToken);

    public async Task<TrackDto?> UpdateAsync(
        int id, int userId, bool isAdmin, UpdateCreativeRequestDto request, CancellationToken cancellationToken = default)
    {
        var entity = await db.Tracks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        CreativeSupport.CheckOwner(entity.UserId, userId, isAdmin);
        entity.Title = CreativeSupport.Title(request.Title);
        entity.Description = CreativeSupport.Description(request.Description);
        entity.TagsCsv = CreativeSupport.Tags(request.Tags);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<TrackDto?> SetCoverAsync(
        int id, int userId, bool isAdmin, Stream cover, string coverFileName, long coverSizeBytes, CancellationToken cancellationToken = default)
    {
        var entity = await db.Tracks.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        CreativeSupport.CheckOwner(entity.UserId, userId, isAdmin);
        var ext = CreativeSupport.CheckFile(coverFileName, coverSizeBytes, CreativeRules.CoverExtensions, CreativeRules.MaxCoverBytes, "Okładka");
        var old = entity.CoverFileName;
        entity.CoverFileName = CreativeStorage.NewStoredName(ext);
        await storage.SaveAsync(Kind, id, entity.CoverFileName, cover, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        storage.Delete(Kind, id, old);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<string?> GetFilePathAsync(int id, CancellationToken cancellationToken = default)
    {
        var name = await db.Tracks.AsNoTracking().Where(x => x.Id == id).Select(x => x.StoredFileName).FirstOrDefaultAsync(cancellationToken);
        return storage.PathFor(Kind, id, name);
    }

    public async Task<string?> GetCoverPathAsync(int id, CancellationToken cancellationToken = default)
    {
        var name = await db.Tracks.AsNoTracking().Where(x => x.Id == id).Select(x => x.CoverFileName).FirstOrDefaultAsync(cancellationToken);
        return storage.PathFor(Kind, id, name);
    }

    private async Task<IReadOnlyList<TrackDto>> ToDtosAsync(IQueryable<TrackEntity> query, CancellationToken cancellationToken)
    {
        var rows = await query.ToListAsync(cancellationToken);
        var authors = await CreativeSupport.AuthorsAsync(db, rows.Select(r => r.UserId), cancellationToken);
        return rows.Select(r => new TrackDto
        {
            Id = r.Id,
            UserId = r.UserId,
            AuthorDisplayName = authors.GetValueOrDefault(r.UserId),
            Title = r.Title,
            Description = r.Description,
            Tags = CsvList.Split(r.TagsCsv),
            FileUrl = $"api/music/{r.Id}/file",
            CoverUrl = r.CoverFileName is null ? null : $"api/music/{r.Id}/cover",
            FileName = r.OriginalFileName,
            SizeBytes = r.SizeBytes,
            CreatedAtUtc = r.CreatedAtUtc
        }).ToList();
    }
}

public sealed class EfStoryService(SgsServerDbContext db, CreativeStorage storage) : IStoryService
{
    private const string Kind = "stories";

    public async Task<IReadOnlyList<StoryDto>> ListAsync(CreativeQuery? query, CancellationToken cancellationToken = default) =>
        await ToDtosAsync(await CreativeSupport.FilterAsync(db, db.Stories.AsNoTracking(), query, cancellationToken), false, cancellationToken);

    public async Task<IReadOnlyList<StoryDto>> ListMineAsync(int userId, CancellationToken cancellationToken = default) =>
        await ToDtosAsync(
            await CreativeSupport.FilterAsync(db, db.Stories.AsNoTracking().Where(x => x.UserId == userId), null, cancellationToken),
            false,
            cancellationToken);

    public async Task<StoryDto?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        (await ToDtosAsync(db.Stories.AsNoTracking().Where(x => x.Id == id), true, cancellationToken)).FirstOrDefault();

    public async Task<StoryDto> CreateAsync(int userId, CreateStoryRequestDto request, CancellationToken cancellationToken = default)
    {
        var title = CreativeSupport.Title(request.Title);
        var description = CreativeSupport.Description(request.Description);
        var tags = CreativeSupport.Tags(request.Tags);
        var chapters = CreativeSupport.BuildChapters(request.Chapters);

        var now = DateTimeOffset.UtcNow;
        var entity = new StoryEntity
        {
            UserId = userId,
            Title = title,
            Description = description,
            TagsCsv = tags,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Chapters = chapters
        };

        db.Stories.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(entity.Id, cancellationToken))!;
    }

    public async Task<StoryDto?> SetCoverAsync(
        int id, int userId, bool isAdmin, Stream cover, string coverFileName, long coverSizeBytes, CancellationToken cancellationToken = default)
    {
        var entity = await db.Stories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        CreativeSupport.CheckOwner(entity.UserId, userId, isAdmin);

        var ext = CreativeSupport.CheckFile(coverFileName, coverSizeBytes, CreativeRules.CoverExtensions, CreativeRules.MaxCoverBytes, "Okładka");
        var old = entity.CoverFileName;
        entity.CoverFileName = CreativeStorage.NewStoredName(ext);
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await storage.SaveAsync(Kind, id, entity.CoverFileName, cover, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        storage.Delete(Kind, id, old);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<CreativeDeleteResult> DeleteAsync(int id, int userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var entity = await db.Stories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return CreativeDeleteResult.NotFound;
        }

        if (entity.UserId != userId && !isAdmin)
        {
            return CreativeDeleteResult.Forbidden;
        }

        db.Stories.Remove(entity);
        await CreativeSupport.RemoveCommentsAsync(db, Kind, id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        storage.DeleteAll(Kind, id);
        return CreativeDeleteResult.Deleted;
    }

    public async Task<IReadOnlyList<StoryDto>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await ToDtosAsync(db.Stories.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id), false, cancellationToken);

    public async Task<StoryDto?> UpdateAsync(
        int id, int userId, bool isAdmin, UpdateStoryRequestDto request, CancellationToken cancellationToken = default)
    {
        var entity = await db.Stories.Include(x => x.Chapters).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        CreativeSupport.CheckOwner(entity.UserId, userId, isAdmin);
        var title = CreativeSupport.Title(request.Title);
        var description = CreativeSupport.Description(request.Description);
        var tags = CreativeSupport.Tags(request.Tags);
        var chapters = CreativeSupport.BuildChapters(request.Chapters);

        // One SaveChanges = one transaction; EF orders the deletes before the inserts for the unique (StoryId, Index) index.
        db.StoryChapters.RemoveRange(entity.Chapters.ToList());
        entity.Title = title;
        entity.Description = description;
        entity.TagsCsv = tags;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        foreach (var c in chapters)
        {
            entity.Chapters.Add(c);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<string?> GetCoverPathAsync(int id, CancellationToken cancellationToken = default)
    {
        var name = await db.Stories.AsNoTracking().Where(x => x.Id == id).Select(x => x.CoverFileName).FirstOrDefaultAsync(cancellationToken);
        return storage.PathFor(Kind, id, name);
    }

    private async Task<IReadOnlyList<StoryDto>> ToDtosAsync(IQueryable<StoryEntity> query, bool withChapters, CancellationToken cancellationToken)
    {
        var rows = await query
            .Select(s => new { Story = s, Count = s.Chapters.Count })
            .ToListAsync(cancellationToken);
        var authors = await CreativeSupport.AuthorsAsync(db, rows.Select(r => r.Story.UserId), cancellationToken);

        var chapters = new Dictionary<int, List<StoryChapterDto>>();
        if (withChapters && rows.Count > 0)
        {
            var ids = rows.Select(r => r.Story.Id).ToList();
            var list = await db.StoryChapters.AsNoTracking()
                .Where(c => ids.Contains(c.StoryId))
                .OrderBy(c => c.Index)
                .ToListAsync(cancellationToken);
            chapters = list
                .GroupBy(c => c.StoryId)
                .ToDictionary(g => g.Key, g => g.Select(c => new StoryChapterDto { Index = c.Index, Title = c.Title, Text = c.Text }).ToList());
        }

        return rows.Select(r => new StoryDto
        {
            Id = r.Story.Id,
            UserId = r.Story.UserId,
            AuthorDisplayName = authors.GetValueOrDefault(r.Story.UserId),
            Title = r.Story.Title,
            Description = r.Story.Description,
            Tags = CsvList.Split(r.Story.TagsCsv),
            CoverUrl = r.Story.CoverFileName is null ? null : $"api/stories/{r.Story.Id}/cover",
            ChapterCount = r.Count,
            CreatedAtUtc = r.Story.CreatedAtUtc,
            UpdatedAtUtc = r.Story.UpdatedAtUtc,
            Chapters = chapters.GetValueOrDefault(r.Story.Id) ?? []
        }).ToList();
    }
}

public sealed class EfCreativeCommentService(SgsServerDbContext db) : ICreativeCommentService
{
    /// <summary>Owner of the item or null when it does not exist (or the section is unknown).</summary>
    private async Task<int?> OwnerAsync(string section, int itemId, CancellationToken cancellationToken) => section switch
    {
        CreativeSections.Art => await db.Artworks.AsNoTracking().Where(x => x.Id == itemId).Select(x => (int?)x.UserId).FirstOrDefaultAsync(cancellationToken),
        CreativeSections.Music => await db.Tracks.AsNoTracking().Where(x => x.Id == itemId).Select(x => (int?)x.UserId).FirstOrDefaultAsync(cancellationToken),
        CreativeSections.Stories => await db.Stories.AsNoTracking().Where(x => x.Id == itemId).Select(x => (int?)x.UserId).FirstOrDefaultAsync(cancellationToken),
        _ => null
    };

    public async Task<IReadOnlyList<CreativeCommentDto>?> ListAsync(
        string section, int itemId, int? viewerUserId, bool viewerIsAdmin, CancellationToken cancellationToken = default)
    {
        var owner = await OwnerAsync(section, itemId, cancellationToken);
        if (owner is null)
        {
            return null;
        }

        var rows = await db.CreativeComments.AsNoTracking()
            .Where(c => c.Section == section && c.ItemId == itemId)
            .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.AuthorUserId).Distinct().ToList();
        var authors = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .ToDictionaryAsync(u => u.Id, cancellationToken);
        return rows.Select(r =>
        {
            var a = authors.GetValueOrDefault(r.AuthorUserId);
            return ToDto(r, a?.DisplayName, a?.AvatarUrl, owner.Value, viewerUserId, viewerIsAdmin);
        }).ToList();
    }

    public async Task<CreativeCommentDto?> AddAsync(
        string section, int itemId, int userId, bool isAdmin, string? text, CancellationToken cancellationToken = default)
    {
        var owner = await OwnerAsync(section, itemId, cancellationToken);
        if (owner is null)
        {
            return null;
        }

        var t = text?.Trim();
        if (string.IsNullOrEmpty(t) || t.Length > CreativeRules.MaxComment)
        {
            throw new ArgumentException($"Komentarz musi mieć od 1 do {CreativeRules.MaxComment} znaków.");
        }

        var entity = new CreativeCommentEntity { Section = section, ItemId = itemId, AuthorUserId = userId, Text = t, CreatedAtUtc = DateTimeOffset.UtcNow };
        db.CreativeComments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var author = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.DisplayName, u.AvatarUrl }).FirstOrDefaultAsync(cancellationToken);
        return ToDto(entity, author?.DisplayName, author?.AvatarUrl, owner.Value, userId, isAdmin);
    }

    public async Task<CreativeDeleteResult> DeleteAsync(
        string section, int itemId, long commentId, int userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var owner = await OwnerAsync(section, itemId, cancellationToken);
        var entity = owner is null
            ? null
            : await db.CreativeComments.FirstOrDefaultAsync(c => c.Id == commentId && c.Section == section && c.ItemId == itemId, cancellationToken);
        if (entity is null)
        {
            return CreativeDeleteResult.NotFound;
        }

        if (entity.AuthorUserId != userId && owner != userId && !isAdmin)
        {
            return CreativeDeleteResult.Forbidden;
        }

        db.CreativeComments.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreativeDeleteResult.Deleted;
    }

    private static CreativeCommentDto ToDto(
        CreativeCommentEntity c, string? name, string? avatar, int ownerId, int? viewerUserId, bool viewerIsAdmin) => new()
    {
        Id = c.Id,
        ItemId = c.ItemId,
        AuthorUserId = c.AuthorUserId,
        AuthorDisplayName = name ?? "Użytkownik",
        AuthorAvatarUrl = avatar,
        Text = c.Text,
        CreatedAtUtc = c.CreatedAtUtc,
        IsMine = viewerUserId == c.AuthorUserId,
        CanDelete = viewerUserId is not null && (viewerUserId == c.AuthorUserId || viewerUserId == ownerId || viewerIsAdmin)
    };
}
