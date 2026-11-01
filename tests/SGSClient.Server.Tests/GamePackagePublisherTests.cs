using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Persistence.Services;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Server.Tests;

public sealed class GamePackagePublisherTests : IDisposable
{
    private const int Owner = 5;
    private const int Stranger = 20;

    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"sgs-publisher-test-{Guid.NewGuid():N}");

    [Fact]
    public async Task PublishZip_ForeignGame_IsRejectedAndLeavesTheOwnersFilesUntouched()
    {
        await using var db = new SgsServerDbContext(new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        db.Games.Add(new GameEntity { Id = 1, UserId = Owner, Title = "Demo", Symbol = "demo" });
        await db.SaveChangesAsync();

        var ownerPackage = Path.Combine(_storageRoot, "demo", "1.0.0", "windows-x64.zip");
        var ownerCover = Path.Combine(_storageRoot, "demo", "media", "cover.png");
        Directory.CreateDirectory(Path.GetDirectoryName(ownerPackage)!);
        Directory.CreateDirectory(Path.GetDirectoryName(ownerCover)!);
        await File.WriteAllTextAsync(ownerPackage, "owner package");
        await File.WriteAllTextAsync(ownerCover, "owner cover");

        using var zip = BuildPackage();
        var result = await CreatePublisher(db).PublishZipAsync(zip, zip.Length, Stranger);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("innego autora"));
        Assert.Equal("owner package", await File.ReadAllTextAsync(ownerPackage));
        Assert.Equal("owner cover", await File.ReadAllTextAsync(ownerCover));
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private GamePackagePublisher CreatePublisher(SgsServerDbContext db)
    {
        var storage = Options.Create(new GameStorageSettings { StoragePath = _storageRoot });
        return new GamePackagePublisher(
            db,
            new GameBuildVersionService(db, storage),
            new EfAchievementService(db, new EfNotificationService(db), new GameMediaService(db, storage)),
            storage);
    }

    /// <summary>A valid package for the symbol and version the owner already published.</summary>
    private static MemoryStream BuildPackage()
    {
        var manifest = new SgsPackageManifest
        {
            SchemaVersion = 1,
            Game = new SgsPackageGameInfo
            {
                Title = "Demo",
                Symbol = "demo",
                Description = "Replaced",
                Genre = "action",
                Engine = "custom",
                Media = new SgsPackageMediaInfo { Cover = "media/cover.png" }
            },
            Build = new SgsPackageBuildInfo
            {
                Version = "1.0.0",
                Platform = "windows",
                Architecture = "x64",
                Executable = "bin/Demo.exe",
                IsDefault = true
            }
        };

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, SgsPackageManifest.FileName, JsonSerializer.Serialize(manifest, SgsPackageManifestSerializer.Options));
            WriteEntry(archive, "bin/Demo.exe", "attacker binary");
            WriteEntry(archive, "media/cover.png", "attacker cover");
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }
}
