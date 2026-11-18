using System.Reflection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Tests;

public sealed class LobbyViewModelTests
{
    private const int Me = 1;

    [Fact]
    public async Task Join_ListsMembersHostFirst_AndFillsFreeSlotsUpToMax()
    {
        var api = FakeApi.Create();
        api.Lobby = Lobby(hostId: 5, members: [5, Me], max: 4);
        var vm = new LobbyViewModel(api.Proxy, new Auth(), null, null);

        await vm.JoinAsync("ABC234");

        Assert.Equal(4, vm.Slots.Count);
        Assert.True(vm.Slots[0].IsHost);
        Assert.Equal(2, vm.Slots.Count(s => s.IsFree));
        Assert.Equal("2 / 4", vm.PlayersText);
        Assert.False(vm.IsHost);
    }

    [Fact]
    public async Task OnlyTheHost_CanStart()
    {
        var api = FakeApi.Create();
        api.Lobby = Lobby(hostId: 5, members: [5, Me], max: 4);
        var guest = new LobbyViewModel(api.Proxy, new Auth(), null, null);
        await guest.JoinAsync("ABC234");

        await guest.StartCommand.ExecuteAsync(null);

        Assert.False(guest.CanStart);
        Assert.DoesNotContain(nameof(IUserApiClient.StartLobbyAsync), api.Calls);

        api.Lobby = Lobby(hostId: Me, members: [Me], max: 4);
        var host = new LobbyViewModel(api.Proxy, new Auth(), null, null);
        await host.CreateAsync(new GameDto { Id = 7, Title = "Gra", Symbol = "G", Features = GameFeatures.Lobby, LobbyMaxPlayers = 4 });

        Assert.True(host.CanStart);
        Assert.Equal(4, api.Created!.MaxPlayers);
    }

    [Fact]
    public async Task Started_LaunchesTheGameOnceWithTheCodeAndHostFlag_AndClosesTheView()
    {
        var api = FakeApi.Create();
        api.Lobby = Lobby(hostId: Me, members: [Me, 5], max: 4);
        var vm = new LobbyViewModel(api.Proxy, new Auth(), null, null);
        var launches = new List<(int, string, bool)>();
        var closed = 0;
        vm.LaunchGame = (id, code, host) =>
        {
            launches.Add((id, code, host));
            return Task.CompletedTask;
        };
        vm.SetViewHandlers(() => { }, () => closed++);
        await vm.JoinAsync("ABC234");

        var started = Lobby(hostId: Me, members: [Me, 5], max: 4, started: true);
        vm.Apply(started);
        vm.Apply(started); // the hub's echo

        Assert.Equal([(7, "ABC234", true)], launches);
        Assert.Equal(1, closed);
        Assert.False(vm.IsActive);
    }

    [Fact]
    public async Task GuestWithoutTheGame_CannotLaunch()
    {
        var api = FakeApi.Create();
        api.Lobby = Lobby(hostId: 5, members: [5, Me], max: 4);
        var vm = new LobbyViewModel(api.Proxy, new Auth(), null, null) { IsGameInstalled = _ => Task.FromResult(false) };
        var launched = false;
        vm.LaunchGame = (_, _, _) =>
        {
            launched = true;
            return Task.CompletedTask;
        };
        await vm.JoinAsync("ABC234");

        Assert.True(vm.NeedsInstall);
        var notices = new List<string>();
        void OnPosted(NotificationDto n) => notices.Add(n.Title);
        NotificationsViewModel.LocalPosted += OnPosted;
        try
        {
            vm.Apply(Lobby(hostId: 5, members: [5, Me], max: 4, started: true));
        }
        finally
        {
            NotificationsViewModel.LocalPosted -= OnPosted;
        }

        Assert.False(launched);
        Assert.Contains("Gra wystartowała bez Ciebie", notices);
    }

    [Fact]
    public async Task AnError_StaysVisible_WhenSomeoneJoins()
    {
        var api = FakeApi.Create();
        api.Lobby = Lobby(hostId: Me, members: [Me], max: 4);
        var vm = new LobbyViewModel(api.Proxy, new Auth(), null, null);
        await vm.JoinAsync("ABC234");

        await vm.StartCommand.ExecuteAsync(null); // the fake API returns no lobby, so Start fails
        var error = vm.StatusText;
        vm.Apply(Lobby(hostId: Me, members: [Me, 5], max: 4));

        Assert.Equal("Nie udało się wystartować.", error);
        Assert.Equal(error, vm.StatusText);
    }

    private static LobbyDto Lobby(int hostId, int[] members, int max, bool started = false) => new()
    {
        Code = "ABC234",
        GameId = 7,
        GameTitle = "Gra",
        HostUserId = hostId,
        Address = string.Empty,
        MaxPlayers = max,
        Players = members.Length,
        IsOpen = true,
        Started = started,
        Members = members.Select(id => new LobbyMemberDto { UserId = id, DisplayName = $"P{id}" }).ToList()
    };

    private sealed class Auth : IAuthSession
    {
        public bool IsAvailable => true;
        public bool IsLoggedIn => true;
        public UserProfileDto? CurrentUser { get; } = new() { Id = Me, AzureSub = "x", DisplayName = "Ja" };
        public string? LastError => null;
        public Task<bool> TrySilentLoginAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> LoginAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> SignUpAsync(nint parentWindowHandle = 0, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>("t");
        public void SetCurrentUser(UserProfileDto? user)
        {
        }
    }

    /// <summary>Only the lobby calls matter; everything else answers with defaults.</summary>
    private class FakeApi : DispatchProxy
    {
        public LobbyDto? Lobby { get; set; }
        public CreateLobbyRequest? Created { get; private set; }
        public List<string> Calls { get; } = [];
        public IUserApiClient Proxy => (IUserApiClient)(object)this;

        public static FakeApi Create() => (FakeApi)(object)DispatchProxy.Create<IUserApiClient, FakeApi>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Calls.Add(targetMethod!.Name);
            if (targetMethod.Name == nameof(IUserApiClient.CreateLobbyAsync))
            {
                Created = (CreateLobbyRequest)args![0]!;
            }

            object? result = targetMethod.Name switch
            {
                nameof(IUserApiClient.CreateLobbyAsync) or nameof(IUserApiClient.JoinLobbyAsync) => (Lobby, (string?)null),
                nameof(IUserApiClient.GetInvitableFriendsAsync) => Array.Empty<InvitableFriendDto>(),
                _ => null
            };
            var taskResult = targetMethod.ReturnType.GetGenericArguments()[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(taskResult)
                .Invoke(null, [result]);
        }
    }
}
