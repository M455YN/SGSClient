using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.Tests;

public class LibraryDiscoveryServiceTests
{
    [Fact]
    public async Task ScanAsync_DiscoversInstallFromSgsInstallRecord()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sgs-scan-test-{Guid.NewGuid():N}");
        var installDir = InstallPathHelper.GetInstallDirectory(libraryRoot, "discovered-game", "2.0.0");
        Directory.CreateDirectory(installDir);

        var exeRelative = "game.exe";
        var exePath = Path.Combine(installDir, exeRelative);
        await File.WriteAllTextAsync(exePath, "stub");

        var record = new SgsInstallRecord
        {
            GameId = 99,
            BuildId = 12,
            Symbol = "discovered-game",
            Title = "Discovered Game",
            Platform = GamePlatform.Windows,
            Architecture = "x64",
            Version = "2.0.0",
            Executable = exeRelative,
            InstalledUtc = DateTimeOffset.UtcNow,
            LibraryRoot = libraryRoot,
            InstallPath = installDir
        };
        var recordJson = JsonSerializer.Serialize(record, SgsInstallRecord.JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(installDir, SgsInstallRecord.FileName), recordJson);
        await WriteLibraryMarkerAsync(libraryRoot, "test-content-id");

        var dbPath = Path.Combine(Path.GetTempPath(), $"sgsclient-scan-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddClientInfrastructure(sqliteDatabasePath: dbPath);
            services.AddSingleton<ILibraryPathProvider>(new FixedLibraryPathProvider(libraryRoot));
            await using var provider = services.BuildServiceProvider();
            await SgsClientDbInitializer.InitializeAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var discovery = scope.ServiceProvider.GetRequiredService<ILibraryDiscoveryService>();
            var result = await discovery.ScanAsync();

            Assert.Equal(1, result.DiscoveredInstalls);

            var store = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var item = await store.GetByGameIdAsync(99);
            Assert.NotNull(item);
            Assert.Equal(InstallState.Installed, item.InstallState);
            Assert.Equal(exePath, item.ExecutablePath);
            Assert.Equal("Discovered Game", item.Title);
        }
        finally
        {
            if (Directory.Exists(libraryRoot))
            {
                Directory.Delete(libraryRoot, recursive: true);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task ScanAsync_DiscoversLegacyFlatLayout()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sgs-legacy-scan-{Guid.NewGuid():N}");
        var installDir = InstallPathHelper.GetLegacyInstallDirectory(libraryRoot, "legacy-game", "1.0.0");
        Directory.CreateDirectory(installDir);

        var exePath = Path.Combine(installDir, "game.exe");
        await File.WriteAllTextAsync(exePath, "stub");

        var record = new SgsInstallRecord
        {
            GameId = 55,
            BuildId = 3,
            Symbol = "legacy-game",
            Title = "Legacy Game",
            Platform = GamePlatform.Windows,
            Version = "1.0.0",
            Executable = "game.exe",
            InstalledUtc = DateTimeOffset.UtcNow
        };
        await File.WriteAllTextAsync(
            Path.Combine(installDir, SgsInstallRecord.FileName),
            JsonSerializer.Serialize(record, SgsInstallRecord.JsonOptions));
        await WriteLibraryMarkerAsync(libraryRoot, "legacy-content-id");

        var dbPath = Path.Combine(Path.GetTempPath(), $"sgsclient-legacy-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddClientInfrastructure(sqliteDatabasePath: dbPath);
            services.AddSingleton<ILibraryPathProvider>(new FixedLibraryPathProvider(libraryRoot));
            await using var provider = services.BuildServiceProvider();
            await SgsClientDbInitializer.InitializeAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var discovery = scope.ServiceProvider.GetRequiredService<ILibraryDiscoveryService>();
            var result = await discovery.ScanAsync();

            Assert.Equal(1, result.DiscoveredInstalls);
        }
        finally
        {
            if (Directory.Exists(libraryRoot))
            {
                Directory.Delete(libraryRoot, recursive: true);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task ScanAsync_TwoBuildsUnderOneGameId_KeepsTheLatestInstall()
    {
        var libraryRoot = Path.Combine(Path.GetTempPath(), $"sgs-dup-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(libraryRoot);
        await WriteLibraryMarkerAsync(libraryRoot, "dup-content-id");
        var now = DateTimeOffset.UtcNow;
        // game-2-build-3.json sorts after game-2-build-10.json, so a last-write-wins scan picked the older install.
        await WriteInstallAsync(libraryRoot, gameId: 2, buildId: 10, "atorth", "1.0.5", now);
        await WriteInstallAsync(libraryRoot, gameId: 2, buildId: 3, "sky-arena", "1.0.0", now.AddHours(-1));

        var dbPath = Path.Combine(Path.GetTempPath(), $"sgsclient-dup-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddClientInfrastructure(sqliteDatabasePath: dbPath);
            services.AddSingleton<ILibraryPathProvider>(new FixedLibraryPathProvider(libraryRoot));
            await using var provider = services.BuildServiceProvider();
            await SgsClientDbInitializer.InitializeAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ILibraryDiscoveryService>().ScanAsync();

            Assert.Equal(1, result.DiscoveredInstalls);
            var item = await scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>().GetByGameIdAsync(2);
            Assert.Equal(("atorth", 10, "1.0.5"), (item!.Symbol, item.BuildId, item.InstalledVersion));
        }
        finally
        {
            if (Directory.Exists(libraryRoot))
            {
                Directory.Delete(libraryRoot, recursive: true);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    private static async Task WriteInstallAsync(
        string libraryRoot, int gameId, int buildId, string symbol, string version, DateTimeOffset installedUtc)
    {
        var installDir = InstallPathHelper.GetInstallDirectory(libraryRoot, symbol, version);
        Directory.CreateDirectory(installDir);
        await File.WriteAllTextAsync(Path.Combine(installDir, "game.exe"), "stub");
        var json = JsonSerializer.Serialize(new SgsInstallRecord
        {
            GameId = gameId,
            BuildId = buildId,
            Symbol = symbol,
            Title = symbol,
            Platform = GamePlatform.Windows,
            Version = version,
            Executable = "game.exe",
            InstalledUtc = installedUtc,
            LibraryRoot = libraryRoot,
            InstallPath = installDir
        }, SgsInstallRecord.JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(installDir, SgsInstallRecord.FileName), json);

        var manifests = SgsLibraryLayout.GetManifestsRoot(libraryRoot);
        Directory.CreateDirectory(manifests);
        await File.WriteAllTextAsync(Path.Combine(manifests, $"game-{gameId}-build-{buildId}.json"), json);
    }

    private static async Task WriteLibraryMarkerAsync(string libraryRoot, string contentId)
    {
        var marker = new SgsLibraryFolderMarker
        {
            ContentId = contentId,
            Label = "Test Library"
        };
        await File.WriteAllTextAsync(
            Path.Combine(libraryRoot, SgsLibraryFolderMarker.FileName),
            JsonSerializer.Serialize(marker, SgsLibraryFolderMarker.JsonOptions));
    }

    private sealed class FixedLibraryPathProvider(string folder) : ILibraryPathProvider
    {
        public IReadOnlyList<string> GetLibraryFolders() => [folder];

        public string GetPrimaryLibraryFolder() => folder;
    }
}
