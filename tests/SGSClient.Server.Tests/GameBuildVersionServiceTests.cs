using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Server.Tests;

public sealed class GameBuildVersionServiceTests : IDisposable
{
    private readonly string _storageRoot;
    private readonly SgsServerDbContext _db;
    private readonly GameBuildVersionService _service;

    public GameBuildVersionServiceTests()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), $"sgs-version-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_storageRoot);

        var options = new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseInMemoryDatabase($"sgs-version-{Guid.NewGuid():N}")
            .Options;
        _db = new SgsServerDbContext(options);

        _service = new GameBuildVersionService(
            _db,
            Options.Create(new GameStorageSettings
            {
                StoragePath = _storageRoot,
                RetentionCount = 3
            }));
    }

    [Fact]
    public async Task RecordPublishedVersion_KeepsOnlyThreeNonCurrentVersions()
    {
        var build = await SeedBuildAsync("demo", "1.0.0");

        for (var i = 2; i <= 6; i++)
        {
            var version = $"{i}.0.0";
            CreatePackage("demo", version, $"{version}.zip");
            build.CurrentVersion = version;
            await _service.RecordPublishedVersionAsync(
                build,
                "demo",
                version,
                "game.exe",
                $"/api/games/packages/demo/{version}/{version}.zip",
                $"{version}.zip");
        }

        var remaining = await _db.GameBuildVersions
            .Where(v => v.GameBuildId == build.Id)
            .OrderBy(v => v.Version)
            .Select(v => v.Version)
            .ToListAsync();

        Assert.Equal(["4.0.0", "5.0.0", "6.0.0"], remaining);
        Assert.False(File.Exists(Path.Combine(_storageRoot, "demo", "1.0.0", "1.0.0.zip")));
        Assert.False(File.Exists(Path.Combine(_storageRoot, "demo", "2.0.0", "2.0.0.zip")));
        Assert.False(File.Exists(Path.Combine(_storageRoot, "demo", "3.0.0", "3.0.0.zip")));
        Assert.True(File.Exists(Path.Combine(_storageRoot, "demo", "4.0.0", "4.0.0.zip")));
    }

    [Fact]
    public async Task RecordPublishedVersion_NeverDeletesCurrentVersionEvenWhenOld()
    {
        var build = await SeedBuildAsync("legacy", "1.0.0");
        CreatePackage("legacy", "1.0.0", "1.0.0.zip");
        await _service.RecordPublishedVersionAsync(
            build,
            "legacy",
            "1.0.0",
            "game.exe",
            "/api/games/packages/legacy/1.0.0/1.0.0.zip",
            "1.0.0.zip");

        for (var i = 2; i <= 5; i++)
        {
            var version = $"{i}.0.0";
            CreatePackage("legacy", version, $"{version}.zip");
            build.CurrentVersion = "1.0.0";
            await _service.RecordPublishedVersionAsync(
                build,
                "legacy",
                version,
                "game.exe",
                $"/api/games/packages/legacy/{version}/{version}.zip",
                $"{version}.zip");
        }

        var versions = await _db.GameBuildVersions
            .Where(v => v.GameBuildId == build.Id)
            .Select(v => v.Version)
            .ToListAsync();

        Assert.Contains("1.0.0", versions);
        Assert.True(File.Exists(Path.Combine(_storageRoot, "legacy", "1.0.0", "1.0.0.zip")));
    }

    private async Task<GameBuildEntity> SeedBuildAsync(string symbol, string version)
    {
        var game = new GameEntity
        {
            UserId = 1,
            Title = "Test",
            Symbol = symbol,
            Description = "Test",
            DraftP = false,
            FeaturedP = false
        };
        _db.Games.Add(game);
        await _db.SaveChangesAsync();

        var build = new GameBuildEntity
        {
            GameId = game.Id,
            Platform = GamePlatform.Windows,
            Architecture = "x64",
            CurrentVersion = version,
            ExeName = "game.exe",
            ZipLink = $"/api/games/packages/{symbol}/{version}/{version}.zip",
            IsDefault = true
        };
        _db.GameBuilds.Add(build);
        await _db.SaveChangesAsync();
        return build;
    }

    private void CreatePackage(string symbol, string version, string fileName)
    {
        var dir = Path.Combine(_storageRoot, symbol, version);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), "zip");
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }
}
