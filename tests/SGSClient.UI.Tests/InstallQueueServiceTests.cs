using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.Tests;

public class InstallQueueServiceTests
{
    [Fact]
    public async Task Enqueue_ProcessesSequentially()
    {
        var installer = new TrackingInstaller();
        var library = new InMemoryLibrary();
        library.Items[1] = MakeItem(1, "One");
        library.Items[2] = MakeItem(2, "Two");

        var services = new ServiceCollection();
        services.AddSingleton<ILocalLibraryStore>(library);
        services.AddSingleton<IGameInstallService>(installer);
        services.AddSingleton(Options.Create(new ClientInstallSettings { AutoUpdateEnabled = false }));
        await using var provider = services.BuildServiceProvider();

        var queue = new InstallQueueService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ClientInstallSettings { AutoUpdateEnabled = false }),
            new FakeUserSettings());

        await queue.EnqueueInstallAsync(1);
        await queue.EnqueueInstallAsync(2);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline
               && installer.StartedOrder.Count < 2)
        {
            await Task.Delay(20);
        }

        Assert.Equal([1, 2], installer.StartedOrder);
        Assert.True(installer.MaxConcurrent <= 1);
    }

    [Fact]
    public async Task CheckForUpdates_SkipsAnInstallWhoseIdBelongsToAnotherGameOnTheServer()
    {
        var installer = new TrackingInstaller();
        var library = new InMemoryLibrary();
        // Game 1 was installed from a test server, where id 1 is a different game than here.
        library.Items[1] = Installed(MakeItem(1, "Sky Arena"), "sky-arena");
        library.Items[2] = Installed(MakeItem(2, "Two"), "g2");

        var api = AchievementDialogFlowTests.FakeApi.Create((method, args) =>
            method.Name == nameof(ISgsApiService.GetGameAsync)
                ? Task.FromResult<GameDto?>(new GameDto
                {
                    Id = (int)args![0]!,
                    Title = "Server game",
                    Symbol = $"g{args[0]}",
                    Builds = [new GameBuildDto { Id = 9, CurrentVersion = "2.0.0", ExeName = "g.exe", ZipLink = "z", IsDefault = true }]
                })
                : null);

        var services = new ServiceCollection();
        services.AddSingleton<ILocalLibraryStore>(library);
        services.AddSingleton<IGameInstallService>(installer);
        services.AddSingleton(api);
        await using var provider = services.BuildServiceProvider();

        var queue = new InstallQueueService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ClientInstallSettings()),
            new FakeUserSettings());

        await queue.CheckForUpdatesAsync();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && installer.StartedOrder.Count < 1)
        {
            await Task.Delay(20);
        }

        await Task.Delay(200);
        Assert.Equal([2], installer.StartedOrder);
    }

    private static GameLibraryItem Installed(GameLibraryItem item, string symbol) => new()
    {
        GameId = item.GameId,
        BuildId = item.BuildId,
        Symbol = symbol,
        Title = item.Title,
        Platform = item.Platform,
        InstallState = InstallState.Installed,
        InstalledVersion = "1.0.0"
    };

    private static GameLibraryItem MakeItem(int id, string title) => new()
    {
        GameId = id,
        BuildId = id,
        Symbol = $"g{id}",
        Title = title,
        Platform = GamePlatform.Windows,
        InstallState = InstallState.NotInstalled
    };

    private sealed class TrackingInstaller : IGameInstallService
    {
        private int _concurrent;
        public int MaxConcurrent { get; private set; }
        public List<int> StartedOrder { get; } = [];

        public string ResolveDownloadUrl(GameBuildDto build) => build.ZipLink;

        public async Task<GameInstallResult> InstallAsync(
            GameLibraryItem item,
            string? version = null,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            StartedOrder.Add(item.GameId);
            var c = Interlocked.Increment(ref _concurrent);
            MaxConcurrent = Math.Max(MaxConcurrent, c);
            await Task.Delay(80, cancellationToken);
            Interlocked.Decrement(ref _concurrent);
            item.InstallState = InstallState.Installed;
            item.InstalledVersion = "1.0.0";
            return GameInstallResult.Ok(item);
        }

        public Task<GameInstallResult> UninstallAsync(
            GameLibraryItem item,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GameInstallResult.Ok(item));

        public Task<GameInstallResult> MoveAsync(
            GameLibraryItem item,
            string destinationLibraryRoot,
            IProgress<InstallProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GameInstallResult.Ok(item));
    }

    private sealed class InMemoryLibrary : ILocalLibraryStore
    {
        public Dictionary<int, GameLibraryItem> Items { get; } = new();

        public Task<IReadOnlyList<GameLibraryItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GameLibraryItem>>(Items.Values.ToList());

        public Task<IReadOnlyList<GameLibraryItem>> GetOwnedAsync(CancellationToken cancellationToken = default) =>
            GetAllAsync(cancellationToken);

        public Task<GameLibraryItem?> GetByGameIdAsync(int gameId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.GetValueOrDefault(gameId));

        public Task<GameLibraryItem?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Values.FirstOrDefault(x => x.Symbol == symbol));

        public Task UpsertAsync(GameLibraryItem item, CancellationToken cancellationToken = default)
        {
            Items[item.GameId] = item;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(int gameId, CancellationToken cancellationToken = default)
        {
            Items.Remove(gameId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserSettings : IClientUserSettingsStore
    {
        public ClientUserSettings Load() => new();

        public Task SaveAsync(ClientUserSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
