using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Persistence;

namespace SGSClient.UI.Tests;

public class ClientInfrastructureTests
{
    [Fact]
    public async Task LocalLibrary_PersistsInstallRecord()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sgsclient-test-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddClientInfrastructure(sqliteDatabasePath: dbPath);
            await using (var provider = services.BuildServiceProvider())
            {
                await SgsClientDbInitializer.InitializeAsync(provider);

                Assert.NotNull(provider.GetService<IGameLauncherService>());

                await using var scope = provider.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();

                await store.UpsertAsync(new GameLibraryItem
                {
                    GameId = 42,
                    BuildId = 7,
                    Symbol = "test-game",
                    Title = "Test Game",
                    Platform = GamePlatform.Linux,
                    Architecture = "x64",
                    InstallState = InstallState.Installed,
                    InstallPath = "/home/user/Games/Test",
                    ExecutablePath = "/home/user/Games/Test/game",
                    InstalledVersion = "1.0.0"
                });

                var loaded = await store.GetBySymbolAsync("test-game");
                Assert.NotNull(loaded);
                Assert.Equal(42, loaded.GameId);
                Assert.Equal(7, loaded.BuildId);
                Assert.Equal(GamePlatform.Linux, loaded.Platform);
                Assert.Equal("Test Game", loaded.Title);
                Assert.Equal(InstallState.Installed, loaded.InstallState);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
