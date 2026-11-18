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
    private string? _error;

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
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(CanToggleReady), nameof(IsGuest))]
    public partial bool IsHost { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(CanToggleReady))]
    public partial bool NeedsInstall { get; private set; }

    /// <summary>Every guest pressed "Gotowy".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    public partial bool AllReady { get; private set; }

    /// <summary>This player (a guest) pressed "Gotowy".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReadyLabel))]
    public partial bool IsReady { get; private set; }

    /// <summary>The match is running: the lobby stays, hidden, until the host's game ends.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart), nameof(CanToggleReady))]
    public partial bool InGame { get; private set; }

    public bool IsGuest => !IsHost;
    public bool CanStart => IsHost && AllReady && !NeedsInstall && !InGame;

    // Without the game a guest cannot be ready, so the host never starts without them.
    public bool CanToggleReady => !IsHost && !NeedsInstall && !InGame;
    public string ReadyLabel => IsReady ? Loc.T("Nie jestem gotowy") : Loc.T("Gotowy");

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

    /// <summary>
    /// Looks the code up first, so a game without the client lobby (it runs its own) is launched with the
    /// code like before, through the same path as an invite.
    /// </summary>
    public async Task JoinByCodeAsync(string code)
    {
        if (_api is null)
        {
            return;
        }

        var lobby = await _api.GetLobbyAsync(code.Trim());
        if (lobby is null)
        {
            NotificationsViewModel.PostLocal(Loc.T("Nie znaleziono lobby"), Loc.T("Sprawdź kod. Lobby mogło już wygasnąć."));
            return;
        }

        await LobbyJoin.RequestAsync(lobby.GameId, lobby.Code);
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

        if (!dto.IsOpen && !dto.Started)
        {
            Reset();
            _close?.Invoke();
            NotificationsViewModel.PostLocal(Loc.T("Lobby zostało zamknięte"));
            return;
        }

        if (_auth?.CurrentUser is { } me && dto.Members.All(m => m.UserId != me.Id))
        {
            var code = dto.Code;
            Reset();
            _close?.Invoke();
            _ = _realtime?.SetLobbyGroupAsync(code, join: false);
            NotificationsViewModel.PostLocal(Loc.T("Host usunął Cię z lobby"));
            return;
        }

        var wasHost = IsHost;
        var joinedNow = dto.Members.Count > current.Members.Count;
        Lobby = dto;
        Code = dto.Code;
        if (string.IsNullOrEmpty(GameTitle))
        {
            GameTitle = dto.GameTitle ?? string.Empty;
        }

        IsHost = IsMe(dto.HostUserId);
        if (IsHost && !wasHost)
        {
            // The host left and this player took over: keep the lobby alive from here on.
            _keepAlive?.Start();
        }

        IsReady = dto.Members.Any(m => IsMe(m.UserId) && m.IsReady);
        AllReady = dto.Members.All(m => m.IsReady);
        var max = Math.Max(dto.MaxPlayers, dto.Members.Count);
        PlayersText = $"{dto.Members.Count} / {max}";

        if (dto.Started && !InGame)
        {
            StartGame(dto);
        }
        else if (!dto.Started && InGame)
        {
            // The host's game ended: same party, back in the lobby for another round.
            InGame = false;
            _show?.Invoke();
        }

        if (Lobby is null)
        {
            return;
        }

        SidebarText = InGame
            ? Loc.F("W grze: {0} {1}/{2}", GameTitle, dto.Members.Count, max)
            : Loc.F("W lobby: {0} {1}/{2}", GameTitle, dto.Members.Count, max);
        StatusText = _error ?? DefaultStatus();

        Slots.Clear();
        for (var i = 0; i < max; i++)
        {
            Slots.Add(i < dto.Members.Count
                ? new LobbySlotItem(
                    dto.Members[i].DisplayName ?? Loc.T("Gracz"),
                    IsHost: dto.Members[i].UserId == dto.HostUserId,
                    IsFree: false)
                {
                    UserId = dto.Members[i].UserId,
                    IsReady = dto.Members[i].IsReady,
                    CanKick = IsHost && !IsMe(dto.Members[i].UserId) && !InGame
                }
                : new LobbySlotItem(Loc.T("Wolne miejsce"), IsHost: false, IsFree: true));
        }

        if (joinedNow)
        {
            _ = RefreshFriendsAsync();
        }
    }

    private void StartGame(LobbyDto dto)
    {
        _close?.Invoke();
        if (NeedsInstall)
        {
            // Reachable only if the game was uninstalled after pressing "Gotowy".
            NotificationsViewModel.PostLocal(
                Loc.T("Gra wystartowała bez Ciebie"),
                Loc.F("Zainstaluj {0}, żeby zagrać ze znajomymi następnym razem.", GameTitle));
            _ = LeaveCurrentAsync();
            return;
        }

        InGame = true;
        _ = LaunchGame?.Invoke(dto.GameId, dto.Code, IsHost);
    }

    /// <summary>
    /// Called when a play session ends. The host's game closing ends the match for everyone,
    /// so games need no code of their own to bring the party back.
    /// </summary>
    public async Task GameEndedAsync(int gameId)
    {
        if (_api is null || !InGame || !IsHost || Lobby is not { } lobby || lobby.GameId != gameId)
        {
            return;
        }

        var (finished, _) = await _api.FinishLobbyAsync(lobby.Code);
        if (finished is not null)
        {
            Post(() => Apply(finished));
        }
    }

    private string DefaultStatus() =>
        InGame ? Loc.T("Trwa mecz")
        : IsHost ? (AllReady ? Loc.T("Wszyscy gotowi, naciśnij Start") : Loc.T("Czekamy, aż wszyscy będą gotowi…"))
        : IsReady ? Loc.T("Czekamy na gospodarza…") : Loc.T("Naciśnij Gotowy, gdy będziesz gotowy");

    [RelayCommand]
    private async Task ToggleReadyAsync()
    {
        if (_api is null || Lobby is not { } lobby || !CanToggleReady)
        {
            return;
        }

        var (updated, error) = await _api.SetLobbyReadyAsync(lobby.Code, !IsReady);
        if (updated is null)
        {
            ShowError(error ?? Loc.T("Nie udało się zmienić gotowości."));
            return;
        }

        _error = null;
        Apply(updated);
    }

    [RelayCommand]
    private async Task KickAsync(LobbySlotItem? slot)
    {
        if (_api is null || Lobby is not { } lobby || slot is not { CanKick: true })
        {
            return;
        }

        var (updated, error) = await _api.KickFromLobbyAsync(lobby.Code, slot.UserId);
        if (updated is null)
        {
            ShowError(error ?? Loc.T("Nie udało się wyrzucić gracza."));
            return;
        }

        _error = null;
        Apply(updated);
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
            ShowError(null);
        }
        else
        {
            ShowError(error ?? Loc.T("Nie udało się wysłać zaproszenia."));
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
            ShowError(error ?? Loc.T("Nie udało się wystartować."));
            return;
        }

        ShowError(null);
        Apply(started);
    }

    /// <summary>Shows an error until the next successful action; null goes back to the normal status.</summary>
    private void ShowError(string? error)
    {
        _error = error;
        StatusText = error ?? DefaultStatus();
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
        InGame = false;
        IsReady = false;
        AllReady = false;
        Slots.Clear();
        Friends.Clear();
        HasFriends = false;
        SidebarText = string.Empty;
        GameTitle = string.Empty;
        _error = null;
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
    public int UserId { get; init; }
    public bool IsReady { get; init; }
    public bool CanKick { get; init; }
    public bool IsTaken => !IsFree;
    public double Opacity => IsFree ? 0.5 : 1;
    public string ReadyText => IsFree || IsHost ? string.Empty : IsReady ? Loc.T("gotowy") : Loc.T("czeka");
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
