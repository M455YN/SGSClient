using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.Tests;

public class GameInstallServiceTests
{
    [Fact]
    public void ResolveDownloadUrl_UsesApiBaseUrlFromSettings()
    {
        var service = CreateService(gamesRoot: Path.GetTempPath());
        var build = new GameBuildDto
        {
            Id = 1,
            GameId = 42,
            Platform = GamePlatform.Windows,
            CurrentVersion = "1.0.0",
            ExeName = "game.exe",
            ZipLink = "/api/games/packages/test/1.0.0/test.zip"
        };

        var url = service.ResolveDownloadUrl(build);

        Assert.Equal(
            "https://localhost:7142/api/games/packages/test/1.0.0/test.zip",
            url);
    }

    [Fact]
    public async Task UninstallAsync_RemovesFilesButKeepsLibraryEntry()
    {
        var gamesRoot = Path.Combine(Path.GetTempPath(), $"sgs-uninstall-test-{Guid.NewGuid():N}");
        var installDir = InstallPathHelper.GetInstallDirectory(gamesRoot, "test-game", "1.0.0");
        Directory.CreateDirectory(installDir);
        var exePath = Path.Combine(installDir, "game.exe");
        await File.WriteAllTextAsync(exePath, "stub");

        var dbPath = Path.Combine(Path.GetTempPath(), $"sgsclient-uninstall-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddClientInfrastructure(sqliteDatabasePath: dbPath);
            await using var provider = services.BuildServiceProvider();
            await SgsClientDbInitializer.InitializeAsync(provider);

            await using var scope = provider.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();

            var item = new GameLibraryItem
            {
                GameId = 42,
                BuildId = 7,
                Symbol = "test-game",
                Title = "Test Game",
                Platform = GamePlatform.Windows,
                Architecture = "x64",
                InstallState = InstallState.Installed,
                InstallPath = installDir,
                ExecutablePath = exePath,
                InstalledVersion = "1.0.0"
            };
            await store.UpsertAsync(item);

            var service = CreateService(gamesRoot);
            var result = await service.UninstallAsync(item);
            await store.UpsertAsync(item);

            Assert.True(result.Success);
            Assert.Equal(InstallState.NotInstalled, item.InstallState);
            Assert.Null(item.InstallPath);
            Assert.Null(item.ExecutablePath);
            Assert.Null(item.InstalledVersion);
            Assert.False(Directory.Exists(installDir));

            var stillInLibrary = await store.GetByGameIdAsync(42);
            Assert.NotNull(stillInLibrary);
            Assert.Equal("Test Game", stillInLibrary.Title);
            Assert.Equal(InstallState.NotInstalled, stillInLibrary.InstallState);
        }
        finally
        {
            if (Directory.Exists(gamesRoot))
            {
                Directory.Delete(gamesRoot, recursive: true);
            }

            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task MoveAsync_MovesFilesToAnotherLibraryFolder()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"sgs-move-src-{Guid.NewGuid():N}");
        var destRoot = Path.Combine(Path.GetTempPath(), $"sgs-move-dst-{Guid.NewGuid():N}");
        var installDir = InstallPathHelper.GetInstallDirectory(sourceRoot, "test-game", "1.0.0");
        Directory.CreateDirectory(installDir);
        var exePath = Path.Combine(installDir, "game.exe");
        await File.WriteAllTextAsync(exePath, "stub");

        try
        {
            var item = new GameLibraryItem
            {
                GameId = 42,
                BuildId = 7,
                Symbol = "test-game",
                Title = "Test Game",
                Platform = GamePlatform.Windows,
                Architecture = "x64",
                InstallState = InstallState.Installed,
                InstallPath = installDir,
                ExecutablePath = exePath,
                InstalledVersion = "1.0.0"
            };

            var service = new GameInstallService(
                new HttpClient(),
                new NullSgsApiService(),
                Options.Create(new ApiClientSettings { BaseUrl = "https://localhost:7142/" }),
                new FixedLibraryPathProvider(sourceRoot, destRoot),
                new NullLibraryStore());

            var result = await service.MoveAsync(item, destRoot);

            var expectedDir = InstallPathHelper.GetInstallDirectory(destRoot, "test-game", "1.0.0");
            Assert.True(result.Success);
            Assert.False(Directory.Exists(installDir));
            Assert.True(Directory.Exists(expectedDir));
            Assert.Equal(expectedDir, item.InstallPath);
            Assert.True(File.Exists(item.ExecutablePath));
            Assert.True(File.Exists(Path.Combine(expectedDir, SgsInstallRecord.FileName)));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(destRoot))
            {
                Directory.Delete(destRoot, recursive: true);
            }
        }
    }

    private static GameInstallService CreateService(string gamesRoot) =>
        new(
            new HttpClient(),
            new NullSgsApiService(),
            Options.Create(new ApiClientSettings { BaseUrl = "https://localhost:7142/" }),
            new FixedLibraryPathProvider(gamesRoot),
            new NullLibraryStore());

    private sealed class FixedLibraryPathProvider(params string[] folders) : ILibraryPathProvider
    {
        public IReadOnlyList<string> GetLibraryFolders() => folders;

        public string GetPrimaryLibraryFolder() => folders[0];
    }

    private sealed class NullLibraryStore : ILocalLibraryStore
    {
        public Task<IReadOnlyList<GameLibraryItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameLibraryItem>>([]);

        public Task<GameLibraryItem?> GetByGameIdAsync(int gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult<GameLibraryItem?>(null);

        public Task<GameLibraryItem?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult<GameLibraryItem?>(null);

        public Task UpsertAsync(GameLibraryItem item, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(int gameId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NullSgsApiService : ISgsApiService
    {
        public Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameDto>>([]);

        public Task<GameDto?> GetGameAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<IReadOnlyList<GameDlcDto>> GetGameDlcsAsync(int gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameDlcDto>>([]);

        public Task<IReadOnlyList<GameDto>> GetMyGamesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameDto>>([]);

        public Task<GameDto?> UpdateGameAsync(int gameId, UpdateGameRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<bool> DeleteGameAsync(int gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<GameDto>> GetAdminGamesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameDto>>([]);

        public Task<GameDto?> UpdateAdminGameAsync(int gameId, UpdateGameRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<bool> DeleteAdminGameAsync(int gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdminUserDto>>([]);

        public Task<AdminRoleUpdateResult> UpdateAdminUserRoleAsync(
            int userId,
            UserRole role,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminRoleUpdateResult { Error = "not supported" });

        public Task<GameDto?> UploadGameCoverAsync(
            int gameId,
            string localFilePath,
            bool asAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<GameDto?> ClearGameCoverAsync(
            int gameId,
            bool asAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<GameDto?> UploadGameHeaderAsync(
            int gameId,
            string localFilePath,
            bool asAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<GameDto?> ClearGameHeaderAsync(
            int gameId,
            bool asAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<GameDto?> UploadGameIntroAsync(
            int gameId,
            string localFilePath,
            bool asAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<GameDto?> ClearGameIntroAsync(
            int gameId,
            bool asAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<GameDto?> AddGameScreenshotAsync(
            int gameId,
            string localFilePath,
            bool asAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<GameDto?> RemoveGameScreenshotAsync(
            int gameId,
            int screenshotId,
            bool asAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameDto?>(null);

        public Task<IReadOnlyList<GameBuildVersionDto>> GetBuildVersionsAsync(
            int gameId,
            int buildId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameBuildVersionDto>>([]);

        public Task<IReadOnlyList<GameBuildVersionDto>> GetOwnedVersionsAsync(
            int gameId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameBuildVersionDto>>([]);

        public Task<GameStatsDto?> GetOwnedGameStatsAsync(
            int gameId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameStatsDto?>(null);

        public Task<GameBuildVersionDto?> UpdateVersionNotesAsync(
            int gameId,
            int versionId,
            string? releaseNotes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameBuildVersionDto?>(null);

        public Task<IReadOnlyList<GameBuildVersionDto>> GetAdminVersionsAsync(
            int gameId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameBuildVersionDto>>([]);

        public Task<GameStatsDto?> GetAdminGameStatsAsync(
            int gameId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameStatsDto?>(null);

        public Task<GameBuildVersionDto?> UpdateAdminVersionNotesAsync(
            int gameId,
            int versionId,
            string? releaseNotes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<GameBuildVersionDto?>(null);
    }
}
