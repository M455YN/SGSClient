using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.Tests;

public class LocalLibraryOwnershipTests
{
    [Fact]
    public async Task GetOwnedAsync_ShowsOnlyGamesOfSignedInAccount_AndKeepsTheOtherInstalls()
    {
        await WithStoreAsync(async (store, _, api) =>
        {
            api.Library = [new GameDto { Id = 2, Title = "Game 2", Symbol = "game-2" }];

            Assert.Equal(new[] { 2 }, await OwnedIdsAsync(store));
            Assert.Equal(2, (await store.GetAllAsync()).Count);
        });
    }

    [Fact]
    public async Task GetOwnedAsync_EmptyServerLibrary_HidesEverything()
    {
        await WithStoreAsync(async (store, _, api) =>
        {
            api.Library = [];

            Assert.Empty(await OwnedIdsAsync(store));
        });
    }

    [Fact]
    public async Task GetOwnedAsync_ApiDown_UsesListSavedForThatAccount()
    {
        await WithStoreAsync(async (store, auth, api) =>
        {
            api.Library = [new GameDto { Id = 2, Title = "Game 2", Symbol = "game-2" }];
            await store.GetOwnedAsync();
            auth.CurrentUser = User("account-b");
            api.Library = [new GameDto { Id = 1, Title = "Game 1", Symbol = "game-1" }];
            await store.GetOwnedAsync();

            api.Library = null;

            Assert.Equal(new[] { 1 }, await OwnedIdsAsync(store));
            auth.CurrentUser = User("account-a");
            Assert.Equal(new[] { 2 }, await OwnedIdsAsync(store));
        });
    }

    [Fact]
    public async Task GetOwnedAsync_ApiDownWithoutSavedList_KeepsInstallsPlayable()
    {
        await WithStoreAsync(async (store, _, api) =>
        {
            api.Library = null;

            Assert.Equal(new[] { 1, 2 }, await OwnedIdsAsync(store));
        });
    }

    [Fact]
    public async Task GetOwnedAsync_SignedOut_ShowsNothing()
    {
        await WithStoreAsync(async (store, auth, api) =>
        {
            api.Library = [new GameDto { Id = 2, Title = "Game 2", Symbol = "game-2" }];
            auth.CurrentUser = null;

            Assert.Empty(await OwnedIdsAsync(store));
        });
    }

    [Fact]
    public async Task UpsertClaimedAsync_KeepsExistingInstall()
    {
        await WithStoreAsync(async (store, _, _) =>
        {
            await store.UpsertClaimedAsync(Item(1, InstallState.NotInstalled));

            var item = await store.GetByGameIdAsync(1);
            Assert.Equal(InstallState.Installed, item!.InstallState);
            Assert.Equal("C:/games/1", item.InstallPath);
        });
    }

    private static async Task<int[]> OwnedIdsAsync(ILocalLibraryStore store) =>
        (await store.GetOwnedAsync()).Select(x => x.GameId).Order().ToArray();

    private static async Task WithStoreAsync(Func<LocalLibraryService, FakeAuth, ApiStub, Task> test)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sgsclient-owned-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddClientInfrastructure(sqliteDatabasePath: dbPath);
        await using var provider = services.BuildServiceProvider();
        await SgsClientDbInitializer.InitializeAsync(provider);

        await using var scope = provider.CreateAsyncScope();
        var auth = new FakeAuth { CurrentUser = User("account-a") };
        var api = DispatchProxy.Create<ISgsApiService, ApiStub>();
        var stub = (ApiStub)(object)api;
        var store = new LocalLibraryService(
            scope.ServiceProvider.GetRequiredService<SgsClientDbContext>(),
            auth,
            api,
            scope.ServiceProvider.GetRequiredService<ICatalogCacheStore>());
        await store.UpsertAsync(Item(1, InstallState.Installed));
        await store.UpsertAsync(Item(2, InstallState.Installed));

        await test(store, auth, stub);
    }

    private static GameLibraryItem Item(int gameId, InstallState state) => new()
    {
        GameId = gameId,
        BuildId = gameId,
        Symbol = $"game-{gameId}",
        Title = $"Game {gameId}",
        InstallState = state,
        InstallPath = state == InstallState.Installed ? $"C:/games/{gameId}" : null
    };

    private static UserProfileDto User(string sub) => new()
    {
        AzureSub = sub,
        DisplayName = sub,
        CreatedAtUtc = DateTimeOffset.UtcNow
    };

    public class ApiStub : DispatchProxy
    {
        public IReadOnlyList<GameDto>? Library { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(ISgsApiService.TryGetMyLibraryAsync)
                ? Task.FromResult(Library)
                : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class FakeAuth : IAuthSession
    {
        public bool IsAvailable => true;
        public bool IsLoggedIn => CurrentUser is not null;
        public UserProfileDto? CurrentUser { get; set; }
        public string? LastError => null;
        public Task<bool> TrySilentLoginAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> LoginAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SignUpAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public void SetCurrentUser(UserProfileDto? user) => CurrentUser = user;
    }
}
