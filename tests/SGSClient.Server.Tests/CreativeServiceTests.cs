using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Persistence.Services;

namespace SGSClient.Server.Tests;

public sealed class CreativeServiceTests : IDisposable
{
    private readonly string _storage = Path.Combine(Path.GetTempPath(), "sgs-creative-" + Guid.NewGuid().ToString("N"));
    private readonly SgsServerDbContext _db;
    private readonly CreativeStorage _files;

    public CreativeServiceTests()
    {
        _db = new SgsServerDbContext(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        _files = new CreativeStorage(Options.Create(new GameStorageSettings { StoragePath = _storage }));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_storage))
        {
            Directory.Delete(_storage, recursive: true);
        }
    }

    private static MemoryStream Bytes() => new([1, 2, 3, 4]);

    [Fact]
    public async Task Story_CreateAndGet_ReturnsOrderedChapters()
    {
        _db.Users.Add(new UserEntity { Id = 7, AzureSub = "s", DisplayName = "Ala", Email = "a@x.pl" });
        await _db.SaveChangesAsync();
        var sut = new EfStoryService(_db, _files);

        var created = await sut.CreateAsync(7, new CreateStoryRequestDto
        {
            Title = "  Opowieść ",
            Tags = ["Fantasy", "fantasy", " Mroczne "],
            Chapters =
            [
                new CreateStoryChapterDto { Title = "Początek", Text = "Raz" },
                new CreateStoryChapterDto { Title = "", Text = "Dwa" }
            ]
        });

        var story = await sut.GetAsync(created.Id);
        Assert.NotNull(story);
        Assert.Equal("Opowieść", story.Title);
        Assert.Equal("Ala", story.AuthorDisplayName);
        Assert.Equal(["fantasy", "mroczne"], story.Tags);
        Assert.Equal(2, story.ChapterCount);
        Assert.Equal([1, 2], story.Chapters.Select(c => c.Index));
        Assert.Equal("Rozdział 2", story.Chapters[1].Title);
        Assert.Empty((await sut.ListAsync(null)).Single().Chapters);
    }

    [Fact]
    public async Task Story_RejectsEmptyChapterText_AndNoChapters()
    {
        var sut = new EfStoryService(_db, _files);
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(1, new CreateStoryRequestDto
        {
            Title = "T",
            Chapters = [new CreateStoryChapterDto { Title = "a", Text = "  " }]
        }));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(1, new CreateStoryRequestDto
        {
            Title = "T",
            Chapters = []
        }));
    }

    [Fact]
    public async Task Art_BadExtensionRejected_GoodOneStored()
    {
        var sut = new EfArtworkService(_db, _files);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.CreateAsync(1, "Evil", null, [], Bytes(), "../../evil.exe", 4));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.CreateAsync(1, " ", null, [], Bytes(), "a.png", 4));
        Assert.Empty(await _db.Artworks.ToListAsync());

        var dto = await sut.CreateAsync(1, "Obraz", null, ["Pixel"], Bytes(), "../../my pic.PNG", 4);
        Assert.Equal("my pic.PNG", dto.FileName);
        var path = await sut.GetFilePathAsync(dto.Id);
        Assert.NotNull(path);
        Assert.StartsWith(Path.GetFullPath(_storage), path);
        Assert.EndsWith(".png", path);

        Assert.Equal(CreativeDeleteResult.Deleted, await sut.DeleteAsync(dto.Id, 1, false));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Art_ListFiltersByTag_AndQuery()
    {
        var sut = new EfArtworkService(_db, _files);
        await sut.CreateAsync(1, "Smok", null, ["fantasy", "dragon"], Bytes(), "a.png", 4);
        await sut.CreateAsync(1, "Miasto", "neonowe", ["scifi"], Bytes(), "b.jpg", 4);

        var byTag = await sut.ListAsync(new CreativeQuery { Tag = "FANTASY" });
        Assert.Equal("Smok", Assert.Single(byTag).Title);
        Assert.Equal("Miasto", Assert.Single(await sut.ListAsync(new CreativeQuery { Q = "neon" })).Title);
        Assert.Empty(await sut.ListAsync(new CreativeQuery { Tag = "fan" }));
        Assert.Equal(["Miasto", "Smok"], (await sut.ListAsync(new CreativeQuery { Sort = "title" })).Select(a => a.Title));
    }

    [Fact]
    public async Task Delete_OnlyOwnerOrAdmin()
    {
        var sut = new EfMusicService(_db, _files);
        var track = await sut.CreateAsync(1, "Song", null, [], Bytes(), "s.mp3", 4, null, null, 0);

        Assert.Equal(CreativeDeleteResult.Forbidden, await sut.DeleteAsync(track.Id, 2, false));
        Assert.NotNull(await sut.GetAsync(track.Id));
        Assert.Equal(CreativeDeleteResult.NotFound, await sut.DeleteAsync(999, 1, false));
        Assert.Equal(CreativeDeleteResult.Deleted, await sut.DeleteAsync(track.Id, 2, true));
        Assert.Null(await sut.GetAsync(track.Id));
    }

    private static CreateStoryRequestDto Story(params string[] texts) => new()
    {
        Title = "S",
        Chapters = texts.Select((t, i) => new CreateStoryChapterDto { Title = "c" + i, Text = t }).ToList()
    };

    [Fact]
    public async Task Comments_AddListDelete_Permissions()
    {
        _db.Users.AddRange(
            new UserEntity { Id = 1, AzureSub = "o", DisplayName = "Owner", Email = "o@x.pl" },
            new UserEntity { Id = 2, AzureSub = "a", DisplayName = "Author", Email = "a@x.pl", AvatarUrl = "/av.png" },
            new UserEntity { Id = 3, AzureSub = "s", DisplayName = "Stranger", Email = "s@x.pl" });
        await _db.SaveChangesAsync();
        var art = await new EfArtworkService(_db, _files).CreateAsync(1, "A", null, [], Bytes(), "a.png", 4);
        var sut = new EfCreativeCommentService(_db);

        Assert.Null(await sut.AddAsync("art", 999, 2, false, "x"));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddAsync("art", art.Id, 2, false, "   "));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddAsync("art", art.Id, 2, false, new string('x', 2001)));

        var c1 = (await sut.AddAsync("art", art.Id, 2, false, "  pierwszy "))!;
        var c2 = (await sut.AddAsync("art", art.Id, 3, false, "drugi"))!;
        Assert.Equal("pierwszy", c1.Text);
        Assert.Equal("Author", c1.AuthorDisplayName);
        Assert.Equal("/av.png", c1.AuthorAvatarUrl);
        Assert.True(c1.IsMine && c1.CanDelete);

        var asAnon = (await sut.ListAsync("art", art.Id, null, false))!;
        Assert.Equal([c1.Id, c2.Id], asAnon.Select(c => c.Id));
        Assert.All(asAnon, c => Assert.False(c.IsMine || c.CanDelete));
        var asOwner = (await sut.ListAsync("art", art.Id, 1, false))!;
        Assert.All(asOwner, c => Assert.True(c.CanDelete && !c.IsMine));
        Assert.Null(await sut.ListAsync("art", 999, null, false));

        Assert.Equal(CreativeDeleteResult.Forbidden, await sut.DeleteAsync("art", art.Id, c1.Id, 3, false));
        Assert.Equal(CreativeDeleteResult.Deleted, await sut.DeleteAsync("art", art.Id, c1.Id, 2, false));
        Assert.Equal(CreativeDeleteResult.Deleted, await sut.DeleteAsync("art", art.Id, c2.Id, 1, false));
        Assert.Equal(CreativeDeleteResult.NotFound, await sut.DeleteAsync("art", art.Id, c2.Id, 1, false));

        var c3 = (await sut.AddAsync("art", art.Id, 2, false, "trzeci"))!;
        Assert.Equal(CreativeDeleteResult.Deleted, await sut.DeleteAsync("art", art.Id, c3.Id, 3, true));
    }

    [Fact]
    public async Task DeletingItem_RemovesItsComments_ButNotOthers()
    {
        var music = new EfMusicService(_db, _files);
        var t1 = await music.CreateAsync(1, "T1", null, [], Bytes(), "s.mp3", 4, null, null, 0);
        var story = await new EfStoryService(_db, _files).CreateAsync(1, Story("x"));
        var sut = new EfCreativeCommentService(_db);
        await sut.AddAsync("music", t1.Id, 1, false, "a");
        await sut.AddAsync("stories", story.Id, 1, false, "b");

        await music.DeleteAsync(t1.Id, 1, false);

        var left = await _db.CreativeComments.SingleAsync();
        Assert.Equal("stories", left.Section);
    }

    [Fact]
    public async Task Story_Update_ReplacesChapters_AndChecksOwner()
    {
        var sut = new EfStoryService(_db, _files);
        var created = await sut.CreateAsync(1, Story("a", "b", "c"));

        var updated = (await sut.UpdateAsync(created.Id, 1, false, new UpdateStoryRequestDto
        {
            Title = " Nowy ",
            Tags = ["X"],
            Chapters = [new CreateStoryChapterDto { Title = "", Text = "jedyny" }, new CreateStoryChapterDto { Title = "d", Text = "drugi" }]
        }))!;

        Assert.Equal("Nowy", updated.Title);
        Assert.Equal(["x"], updated.Tags);
        Assert.Equal(["jedyny", "drugi"], updated.Chapters.Select(c => c.Text));
        Assert.Equal([1, 2], updated.Chapters.Select(c => c.Index));
        Assert.Equal(2, await _db.StoryChapters.CountAsync());

        var request = new UpdateStoryRequestDto { Title = "T", Chapters = [new CreateStoryChapterDto { Title = "a", Text = "a" }] };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.UpdateAsync(created.Id, 2, false, request));
        Assert.NotNull(await sut.UpdateAsync(created.Id, 2, true, request));
        Assert.Null(await sut.UpdateAsync(999, 1, false, request));
    }

    [Fact]
    public async Task Update_RejectsEmptyTitle_AndKeepsOldData()
    {
        var sut = new EfArtworkService(_db, _files);
        var art = await sut.CreateAsync(1, "Stary", null, [], Bytes(), "a.png", 4);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.UpdateAsync(art.Id, 1, false, new UpdateCreativeRequestDto { Title = "  " }));
        Assert.Equal("Stary", (await sut.GetAsync(art.Id))!.Title);

        var ok = await sut.UpdateAsync(art.Id, 1, false, new UpdateCreativeRequestDto { Title = "Nowy", Description = "d", Tags = ["A"] });
        Assert.Equal(("Nowy", "d"), (ok!.Title, ok.Description));
    }

    [Fact]
    public async Task Music_SetCover_ReplacesOldFile_AdminAllowed()
    {
        var sut = new EfMusicService(_db, _files);
        var track = await sut.CreateAsync(1, "S", null, [], Bytes(), "s.mp3", 4, Bytes(), "c.png", 4);
        var oldCover = await sut.GetCoverPathAsync(track.Id);
        Assert.NotNull(oldCover);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.SetCoverAsync(track.Id, 2, false, Bytes(), "n.jpg", 4));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.SetCoverAsync(track.Id, 1, false, Bytes(), "n.exe", 4));
        Assert.NotNull(await sut.SetCoverAsync(track.Id, 2, true, Bytes(), "n.jpg", 4));

        Assert.False(File.Exists(oldCover));
        Assert.EndsWith(".jpg", await sut.GetCoverPathAsync(track.Id));
    }

    [Fact]
    public async Task ListAll_IncludesEveryAuthor_NewestFirst()
    {
        var sut = new EfArtworkService(_db, _files);
        var a = await sut.CreateAsync(1, "Moje", null, [], Bytes(), "a.png", 4);
        var b = await sut.CreateAsync(2, "Cudze", null, [], Bytes(), "b.png", 4);

        Assert.Equal([b.Id, a.Id], (await sut.ListAllAsync()).Select(x => x.Id));
        Assert.Single(await sut.ListMineAsync(1));
        await new EfMusicService(_db, _files).CreateAsync(3, "M", null, [], Bytes(), "m.mp3", 4, null, null, 0);
        await new EfStoryService(_db, _files).CreateAsync(3, Story("x"));
        Assert.Single(await new EfMusicService(_db, _files).ListAllAsync());
        Assert.Single(await new EfStoryService(_db, _files).ListAllAsync());
    }
}
