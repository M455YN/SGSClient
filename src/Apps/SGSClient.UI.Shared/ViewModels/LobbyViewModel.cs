using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.ViewModels;

/// <summary>
/// The lobby the player is in right now (one at a time): members, friends to invite, Start for the host.
/// Updates arrive over the social SignalR hub; when the host starts, every member's client launches the game.
/// </summary>
public partial class LobbyViewModel : ObservableObject
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromMinutes(2);

    private readonly IUserApiClient? _api;
    private readonly IAuthSession? _auth;
    private readonly SocialRealtimeService? _realtime;
    private readonly IUiDispatcher? _ui;
    private readonly IUiTimer? _keepAlive;
    private Action? _show;
    private Action? _close;

    public LobbyViewModel()
        : this(null, null, null, null)
    {
    }

    public LobbyViewModel(
        IUserApiClient? api,
        IAuthSession? auth,
        SocialRealtimeService? realtime,
        IUiDispatcher? ui)
    {
        _api = api;
        _auth = auth;
        _realtime = realtime;
        _ui = ui;
        _keepAlive = ui?.CreateTimer(KeepAliveInterval, KeepAliveAsync);
        if (_realtime is not null)
        {
            _realtime.LobbyUpdated += dto => Post(() => Apply(dto));
            // a reconnect starts a fresh connection with no groups
            _realtime.ConnectionChanged += connected =>
            {
                if (connected && Lobby is { } lobby)
                {
                    _ = _realtime.SetLobbyGroupAsync(lobby.Code, join: true);
                }
            };
        }
    }

    /// <summary>Launches the game for this player (game id, lobby code, is host); set by the shell sidebar.</summary>
    public Func<int, string, bool, Task>? LaunchGame { get; set; }

    /// <summary>True when the game can be started on this PC; set by the shell sidebar.</summary>
    public Func<int, Task<bool>>? IsGameInstalled { get; set; }

    /// <summary>Opens the game's store page so a guest can install it; set by the shell sidebar.</summary>
    public Action<int>? OpenGameDetails { get; set; }

    public void SetViewHandlers(Action show, Action close)
    {
        _show = show;
        _close = close;
    }

    public ObservableCollection<LobbySlotItem> Slots { get; } = [];
    public ObservableCollection<LobbyFriendItem> Friends { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    public partial LobbyDto? Lobby { get; private set; }

    public bool IsActive => Lobby is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    public partial bool IsHost { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    public partial bool NeedsInstall { get; private set; }

    public bool CanStart => IsHost && !NeedsInstall;

    [ObservableProperty]
    public partial string GameTitle { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Code { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string PlayersText { get; private set; } = string.Empty;

    /// <summary>"W lobby: Gra 2/4" for the sidebar.</summary>
    [ObservableProperty]
    public partial string SidebarText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasFriends { get; private set; }

    /// <summary>Opens a new lobby for an installed game and shows it.</summary>
    public async Task CreateAsync(GameDto game)
    {
        if (_api is null)
        {
            return;
        }

        await LeaveCurrentAsync();
        var (lobby, error) = await _api.CreateLobbyAsync(new CreateLobbyRequest
        {
            GameId = game.Id,
            MaxPlayers = game.LobbyMaxPlayers ?? 4,
            Players = 1
        });
        if (lobby is null)
        {
            NotificationsViewModel.PostLocal(Loc.T("Nie udało się utworzyć lobby"), error);
            return;
        }

        await EnterAsync(lobby, game.Title);
    }

    /// <summary>Joins a friend's lobby (from an invite or the friends list) and shows it.</summary>
    public async Task JoinAsync(string code)
    {
        if (_api is null)
        {
            return;
        }

        if (Lobby is { } current && string.Equals(current.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            _show?.Invoke();
            return;
        }

        await LeaveCurrentAsync();
        var (lobby, error) = await _api.JoinLobbyAsync(code);
        if (lobby is null)
        {
            NotificationsViewModel.PostLocal(Loc.T("Nie udało się dołączyć do lobby"), error);
            return;
        }

        await EnterAsync(lobby, null);
    }

    /// <summary>Brings the lobby view back, e.g. from the sidebar.</summary>
    [RelayCommand]
    private void Show()
    {
        if (IsActive)
        {
            _show?.Invoke();
        }
    }

    private async Task EnterAsync(LobbyDto lobby, string? title)
    {
        GameTitle = title ?? lobby.GameTitle ?? string.Empty;
        StatusText = string.Empty;
        Friends.Clear();
        HasFriends = false;
        Lobby = lobby;
        Apply(lobby);
        if (Lobby is null)
        {
            return;
        }

        NeedsInstall = IsGameInstalled is not null && !await IsGameInstalled(lobby.GameId);
        if (IsHost)
        {
            _keepAlive?.Start();
        }

        _show?.Invoke();
        if (_realtime is not null)
        {
            await _realtime.SetLobbyGroupAsync(lobby.Code, join: true);
        }

        await RefreshFriendsAsync();
    }

    /// <summary>Applies a lobby snapshot from the API or the hub. Public so tests can feed hub updates.</summary>
    public void Apply(LobbyDto dto)
    {
        // also drops the hub's echo of a start or close this client already handled
        if (Lobby is not { } current || !string.Equals(current.Code, dto.Code, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (dto.Started)
        {
            var host = IsHost || IsMe(dto.HostUserId);
            var canLaunch = !NeedsInstall;
            Reset();
            _close?.Invoke();
            if (canLaunch)
            {
                _ = LaunchGame?.Invoke(dto.GameId, dto.Code, host);
            }

            return;
        }

        if (!dto.IsOpen)
        {
            Reset();
            _close?.Invoke();
            NotificationsViewModel.PostLocal(Loc.T("Lobby zostało zamknięte"));
            return;
        }

        var joinedNow = dto.Members.Count > current.Members.Count;
        Lobby = dto;
        Code = dto.Code;
        if (string.IsNullOrEmpty(GameTitle))
        {
            GameTitle = dto.GameTitle ?? string.Empty;
        }

        IsHost = IsMe(dto.HostUserId);
        var max = Math.Max(dto.MaxPlayers, dto.Members.Count);
        PlayersText = $"{dto.Members.Count} / {max}";
        SidebarText = Loc.F("W lobby: {0} {1}/{2}", GameTitle, dto.Members.Count, max);
        StatusText = IsHost ? Loc.T("Zaproś znajomych i naciśnij Start") : Loc.T("Czekamy na gospodarza…");

        Slots.Clear();
        for (var i = 0; i < max; i++)
        {
            Slots.Add(i < dto.Members.Count
                ? new LobbySlotItem(dto.Members[i].DisplayName ?? Loc.T("Gracz"), IsHost: i == 0, IsFree: false)
                : new LobbySlotItem(Loc.T("Wolne miejsce"), IsHost: false, IsFree: true));
        }

        if (joinedNow)
        {
            _ = RefreshFriendsAsync();
        }
    }

    [RelayCommand]
    private async Task RefreshFriendsAsync()
    {
        if (_api is null || Lobby is not { } lobby)
        {
            return;
        }

        IReadOnlyList<InvitableFriendDto> list;
        try
        {
            list = await _api.GetInvitableFriendsAsync(lobby.Code);
        }
        catch
        {
            return;
        }

        var invited = Friends.Where(f => f.IsInvited).Select(f => f.UserId).ToHashSet();
        Friends.Clear();
        foreach (var friend in list.OrderByDescending(f => f.IsOnline).ThenBy(f => f.DisplayName))
        {
            Friends.Add(new LobbyFriendItem(friend.UserId, friend.DisplayName, friend.IsOnline)
            {
                IsInvited = invited.Contains(friend.UserId)
            });
        }

        HasFriends = Friends.Count > 0;
    }

    [RelayCommand]
    private async Task InviteAsync(LobbyFriendItem? friend)
    {
        if (_api is null || Lobby is not { } lobby || friend is null || friend.IsInvited)
        {
            return;
        }

        var (ok, error) = await _api.InviteToLobbyAsync(lobby.Code, friend.UserId);
        if (ok)
        {
            friend.IsInvited = true;
        }
        else
        {
            StatusText = error ?? Loc.T("Nie udało się wysłać zaproszenia.");
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (_api is null || Lobby is not { } lobby || !CanStart)
        {
            return;
        }

        var (started, error) = await _api.StartLobbyAsync(lobby.Code);
        if (started is null)
        {
            StatusText = error ?? Loc.T("Nie udało się wystartować.");
            return;
        }

        Apply(started);
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        await LeaveCurrentAsync();
        _close?.Invoke();
    }

    [RelayCommand]
    private void Install()
    {
        if (Lobby is { } lobby)
        {
            OpenGameDetails?.Invoke(lobby.GameId);
        }
    }

    private async Task LeaveCurrentAsync()
    {
        if (Lobby is not { } lobby)
        {
            return;
        }

        Reset();
        try
        {
            if (_realtime is not null)
            {
                await _realtime.SetLobbyGroupAsync(lobby.Code, join: false);
            }

            if (_api is not null)
            {
                await _api.LeaveLobbyAsync(lobby.Code);
            }
        }
        catch
        {
            // the server drops idle lobbies on its own
        }
    }

    private void Reset()
    {
        _keepAlive?.Stop();
        Lobby = null;
        IsHost = false;
        NeedsInstall = false;
        Slots.Clear();
        Friends.Clear();
        HasFriends = false;
        SidebarText = string.Empty;
        GameTitle = string.Empty;
    }

    private async Task KeepAliveAsync()
    {
        if (_api is not null && IsHost && Lobby is { } lobby)
        {
            try
            {
                await _api.UpdateLobbyAsync(lobby.Code, new UpdateLobbyRequest());
            }
            catch
            {
                // next tick tries again
            }
        }
    }

    private bool IsMe(int userId) => _auth?.CurrentUser?.Id == userId;

    private void Post(Action action)
    {
        if (_ui is null)
        {
            action();
        }
        else
        {
            _ui.Post(action);
        }
    }
}

public sealed record LobbySlotItem(string Name, bool IsHost, bool IsFree)
{
    public bool IsTaken => !IsFree;
    public double Opacity => IsFree ? 0.5 : 1;
}

public sealed partial class LobbyFriendItem(int userId, string displayName, bool isOnline) : ObservableObject
{
    public int UserId { get; } = userId;
    public string DisplayName { get; } = displayName;
    public bool IsOnline { get; } = isOnline;
    public double Opacity => IsOnline ? 1 : 0.5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InviteLabel), nameof(CanInvite))]
    public partial bool IsInvited { get; set; }

    public string InviteLabel => IsInvited ? Loc.T("Zaproszony") : Loc.T("Zaproś");
    public bool CanInvite => !IsInvited;
}
