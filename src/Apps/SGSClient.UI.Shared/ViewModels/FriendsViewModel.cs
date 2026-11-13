using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.ViewModels;

public partial class FriendsViewModel : ObservableObject
{
    private readonly IUserApiClient? _userApi;
    private readonly IAuthSession? _auth;
    private readonly SocialRealtimeService? _realtime;
    private readonly IUiTimer? _chatPollTimer;
    private readonly IUiDispatcher? _dispatcher;
    private Func<ChatModalViewModel, Task>? _showChat;
    private Dictionary<int, LobbyDto> _friendLobbies = [];
    private bool _realtimeWired;

    public FriendsViewModel(
        IUserApiClient? userApi = null,
        IAuthSession? auth = null,
        SocialRealtimeService? realtime = null,
        IUiDispatcher? dispatcher = null)
    {
        _userApi = userApi;
        _auth = auth;
        _realtime = realtime;
        StatusText = auth?.IsAvailable != true
            ? Loc.T("Zaloguj się, żeby zobaczyć znajomych.")
            : Loc.T("Ładowanie…");

        _dispatcher = dispatcher;
        _chatPollTimer = dispatcher?.CreateTimer(TimeSpan.FromSeconds(5), PollChatAsync);

        WireRealtime();
    }

    private void WireRealtime()
    {
        if (_realtime is null || _realtimeWired)
        {
            return;
        }

        _realtimeWired = true;
        _realtime.MessageReceived += OnRealtimeMessage;
        _realtime.ConnectionChanged += connected =>
        {
            void Apply()
            {
                if (!ShowChatTab)
                {
                    return;
                }

                if (connected)
                {
                    _chatPollTimer?.Stop();
                }
                else
                {
                    _chatPollTimer?.Start();
                }
            }

            RunOnUi(Apply);
        };
    }

    /// <summary>The view shows the conversation in a dialog and completes when it is closed.</summary>
    public void SetShowChatHandler(Func<ChatModalViewModel, Task> show) => _showChat = show;

    private Action<int>? _openFullProfile;

    /// <summary>The shell's full-page profile; opening a person in this panel goes there.</summary>
    public void SetOpenFullProfileHandler(Action<int> open) => _openFullProfile = open;

    // An open conversation dialog handles its own messages; this only keeps the thread list fresh.
    private void OnRealtimeMessage(ChatMessageDto message) => RunOnUi(() => _ = LoadChatThreadsAsync());

    private void RunOnUi(Action action)
    {
        if (_dispatcher is null)
        {
            action();
        }
        else
        {
            _dispatcher.Post(action);
        }
    }

    public ObservableCollection<SocialPersonItem> Friends { get; } = [];
    public ObservableCollection<SocialPersonItem> Incoming { get; } = [];
    public ObservableCollection<SocialPersonItem> Outgoing { get; } = [];
    public ObservableCollection<SocialPersonItem> Following { get; } = [];
    public ObservableCollection<SocialPersonItem> Followers { get; } = [];
    public ObservableCollection<SocialPersonItem> SearchResults { get; } = [];
    public ObservableCollection<SocialPersonItem> OnlineFriends { get; } = [];
    public ObservableCollection<SocialPersonItem> OfflineFriends { get; } = [];
    public ObservableCollection<ChatThreadItem> ChatThreads { get; } = [];

    [ObservableProperty]
    public partial string SelectedTab { get; set; } = "Friends";

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial int IncomingCount { get; set; }

    [ObservableProperty]
    public partial bool OnlineExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool OfflineExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool IncomingExpanded { get; set; } = true;

    [ObservableProperty]
    public partial ChatThreadItem? SelectedChatThread { get; set; }

    [ObservableProperty]
    public partial string ChatStatus { get; set; } = string.Empty;

    public bool ShowFriendsTab => SelectedTab == "Friends";
    public bool ShowRequestsTab => SelectedTab == "Requests";
    public bool ShowFollowingTab => SelectedTab == "Following";
    public bool ShowFollowersTab => SelectedTab == "Followers";
    public bool ShowSearchTab => SelectedTab == "Search";
    public bool ShowChatTab => SelectedTab == "Chat";
    public bool ShowMainList => SelectedTab is not "Chat";
    public bool ShowEmptyFriends => ShowFriendsTab && Friends.Count == 0;
    public bool ShowEmptyFollowing => ShowFollowingTab && Following.Count == 0;
    public bool ShowEmptyFollowers => ShowFollowersTab && Followers.Count == 0;
    public bool ShowEmptySearch => ShowSearchTab && SearchResults.Count == 0 && !IsBusy;
    public bool ShowEmptyChat => ShowChatTab && ChatThreads.Count == 0;
    public bool HasIncoming => Incoming.Count > 0;
    public bool HasOutgoing => Outgoing.Count > 0;
    public bool ShowFriendsGroups => ShowFriendsTab && Friends.Count > 0;
    public int OnlineCount => OnlineFriends.Count;
    public int OfflineCount => OfflineFriends.Count;
    public string RequestsTabLabel => IncomingCount > 0 ? $"Zaproszenia ({IncomingCount})" : Loc.T("Zaproszenia");
    public string SearchPlaceholder => ShowSearchTab ? Loc.T("Szukaj graczy…") : Loc.T("Szukaj znajomych…");

    public async Task InitializeAsync(string? tab = null)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            SelectedTab = tab;
        }

        if (_realtime is not null)
        {
            await _realtime.EnsureConnectedAsync();
        }

        await LoadAsync();
        if (ShowChatTab)
        {
            await LoadChatThreadsAsync();
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (ShowFriendsTab)
        {
            RebuildPresenceLists();
        }
    }

    partial void OnSelectedTabChanged(string value)
    {
        NotifyLists();
        RebuildPresenceLists();
        if (value == "Chat")
        {
            _ = LoadChatThreadsAsync();
            _ = EnsureChatRealtimeOrPollAsync();
        }
        else
        {
            _chatPollTimer?.Stop();
        }
    }

    private async Task EnsureChatRealtimeOrPollAsync()
    {
        if (_realtime is not null)
        {
            await _realtime.EnsureConnectedAsync();
            if (_realtime.IsConnected)
            {
                _chatPollTimer?.Stop();
                return;
            }
        }

        _chatPollTimer?.Start();
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ShowEmptySearch));

    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            SelectedTab = tab;
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_userApi is null || _auth?.IsAvailable != true)
        {
            StatusText = Loc.T("Zaloguj się w Profilu, żeby dodać znajomych.");
            return;
        }

        IsBusy = true;
        try
        {
            var overview = await _userApi.GetSocialAsync();
            if (overview is null)
            {
                StatusText = Loc.T("Nie udało się pobrać znajomych. Zaloguj się ponownie.");
                return;
            }

            // Before ApplyOverview: the rows are plain objects, so lobby info must be set before they are shown.
            try
            {
                _friendLobbies = (await _userApi.GetFriendLobbiesAsync()).ToDictionary(l => l.HostUserId);
            }
            catch
            {
                _friendLobbies = [];
            }

            ApplyOverview(overview);
            StatusText = overview.IncomingCount > 0
                ? Loc.F("{0} oczekujących zaproszeń.", overview.IncomingCount)
                : Loc.F("{0} znajomych", overview.FriendsCount);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        SelectedTab = "Search";
        if (_userApi is null)
        {
            StatusText = Loc.T("Brak klienta API.");
            return;
        }

        var q = SearchQuery.Trim();
        if (q.Length < 2)
        {
            StatusText = Loc.T("Wpisz co najmniej 2 znaki.");
            return;
        }

        IsBusy = true;
        try
        {
            var results = await _userApi.SearchUsersAsync(q);
            SearchResults.Clear();
            foreach (var user in results)
            {
                SearchResults.Add(SocialPersonItem.FromDto(user));
            }

            StatusText = SearchResults.Count == 0
                ? Loc.T("Brak graczy o tej nazwie.")
                : $"Znaleziono {SearchResults.Count}.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyLists();
        }
    }

    [RelayCommand]
    private void OpenProfile(SocialPersonItem? person)
    {
        if (person is not null)
        {
            _openFullProfile?.Invoke(person.UserId);
        }
    }

    [RelayCommand]
    private async Task LoadChatThreadsAsync()
    {
        if (_userApi is null || _auth?.IsAvailable != true)
        {
            ChatStatus = Loc.T("Zaloguj się, żeby pisać.");
            return;
        }

        try
        {
            var threads = await _userApi.GetChatThreadsAsync();
            ChatThreads.Clear();
            foreach (var t in threads)
            {
                ChatThreads.Add(ChatThreadItem.FromDto(t));
            }

            if (ChatThreads.Count == 0 && Friends.Count > 0)
            {
                foreach (var friend in Friends.Take(20))
                {
                    ChatThreads.Add(ChatThreadItem.FromFriend(friend));
                }
            }

            ChatStatus = ChatThreads.Count == 0
                ? Loc.T("Dodaj znajomych, żeby zacząć czat.")
                : string.Empty;
            OnPropertyChanged(nameof(ShowEmptyChat));
        }
        catch (Exception ex)
        {
            ChatStatus = ex.Message;
        }
    }

    [RelayCommand]
    private async Task OpenChatThreadAsync(ChatThreadItem? thread)
    {
        if (thread is null)
        {
            return;
        }

        await OpenChatWithPeerIdAsync(thread.PeerUserId, thread.Title);
    }

    [RelayCommand]
    private async Task OpenChatWithAsync(SocialPersonItem? person)
    {
        if (person is null)
        {
            return;
        }

        await OpenChatWithPeerIdAsync(person.UserId, person.Title);
    }

    [RelayCommand]
    private Task AddFriendAsync(SocialPersonItem? person) =>
        MutateAsync(person, id => _userApi!.SendFriendRequestAsync(id));

    [RelayCommand]
    private Task AcceptFriendAsync(SocialPersonItem? person) =>
        MutateAsync(person, id => _userApi!.AcceptFriendRequestAsync(id));

    [RelayCommand]
    private Task RemoveFriendAsync(SocialPersonItem? person) =>
        MutateAsync(person, id => _userApi!.RemoveFriendAsync(id));

    [RelayCommand]
    private Task FollowAsync(SocialPersonItem? person) =>
        MutateAsync(person, id => _userApi!.FollowAsync(id));

    [RelayCommand]
    private Task UnfollowAsync(SocialPersonItem? person) =>
        MutateAsync(person, id => _userApi!.UnfollowAsync(id));

    private async Task OpenChatWithPeerIdAsync(int peerUserId, string title)
    {
        if (_userApi is null || _showChat is null)
        {
            return;
        }

        using var chat = new ChatModalViewModel(_userApi, _realtime, _dispatcher, peerUserId, title);
        _ = chat.LoadAsync();
        await _showChat(chat);
        await chat.MarkLiveMessagesReadAsync();
        await LoadChatThreadsAsync(); // reading clears unread counts
    }

    private async Task PollChatAsync()
    {
        if (ShowChatTab)
        {
            await LoadChatThreadsAsync();
        }
    }

    private async Task MutateAsync(
        SocialPersonItem? person,
        Func<int, Task<(SocialOverviewDto? Overview, string? Error)>> action)
    {
        if (person is null || _userApi is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (overview, error) = await action(person.UserId);
            if (overview is null)
            {
                StatusText = error ?? Loc.T("Nie udało się zapisać.");
                return;
            }

            ApplyOverview(overview);
            if (ShowSearchTab && !string.IsNullOrWhiteSpace(SearchQuery))
            {
                await SearchAsync();
                return;
            }

            StatusText = Loc.T("Zapisano.");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task JoinLobbyAsync(SocialPersonItem? person)
    {
        if (person?.Lobby is { } lobby)
        {
            await LobbyJoin.RequestAsync(lobby.GameId, lobby.Code);
        }
    }

    private void ApplyOverview(SocialOverviewDto overview)
    {
        Replace(Friends, overview.Friends);
        foreach (var friend in Friends)
        {
            friend.Lobby = _friendLobbies.GetValueOrDefault(friend.UserId);
        }

        Replace(Incoming, overview.Incoming);
        Replace(Outgoing, overview.Outgoing);
        Replace(Following, overview.Following);
        Replace(Followers, overview.Followers);
        IncomingCount = overview.IncomingCount;
        RebuildPresenceLists();
        NotifyLists();
    }

    private void RebuildPresenceLists()
    {
        OnlineFriends.Clear();
        OfflineFriends.Clear();
        var q = SearchQuery.Trim();
        foreach (var friend in Friends)
        {
            if (q.Length > 0 && !friend.Matches(q))
            {
                continue;
            }

            if (friend.IsOnline)
            {
                OnlineFriends.Add(friend);
            }
            else
            {
                OfflineFriends.Add(friend);
            }
        }

        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(OfflineCount));
        OnPropertyChanged(nameof(ShowFriendsGroups));
    }

    private static void Replace(ObservableCollection<SocialPersonItem> target, IReadOnlyList<SocialUserDto> source)
    {
        target.Clear();
        foreach (var user in source)
        {
            target.Add(SocialPersonItem.FromDto(user));
        }
    }

    private void NotifyLists()
    {
        OnPropertyChanged(nameof(ShowEmptyFriends));
        OnPropertyChanged(nameof(ShowEmptyFollowing));
        OnPropertyChanged(nameof(ShowEmptyFollowers));
        OnPropertyChanged(nameof(ShowEmptySearch));
        OnPropertyChanged(nameof(ShowFriendsGroups));
        OnPropertyChanged(nameof(ShowFriendsTab));
        OnPropertyChanged(nameof(ShowRequestsTab));
        OnPropertyChanged(nameof(ShowFollowingTab));
        OnPropertyChanged(nameof(ShowFollowersTab));
        OnPropertyChanged(nameof(ShowSearchTab));
        OnPropertyChanged(nameof(ShowChatTab));
        OnPropertyChanged(nameof(ShowMainList));
        OnPropertyChanged(nameof(ShowEmptyChat));
        OnPropertyChanged(nameof(HasIncoming));
        OnPropertyChanged(nameof(HasOutgoing));
        OnPropertyChanged(nameof(RequestsTabLabel));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(OfflineCount));
    }
}

public sealed class ProfileCommentItem
{
    public long Id { get; init; }
    public int AuthorUserId { get; init; }
    public required string AuthorLabel { get; init; }
    public required string Text { get; init; }
    public required string WhenText { get; init; }
    public required string Initials { get; init; }
    public bool CanDelete { get; init; }

    public static ProfileCommentItem FromDto(ProfileCommentDto dto) => new()
    {
        Id = dto.Id,
        AuthorUserId = dto.AuthorUserId,
        AuthorLabel = string.IsNullOrWhiteSpace(dto.AuthorPlayerTag)
            ? dto.AuthorDisplayName
            : $"{dto.AuthorDisplayName} (@{dto.AuthorPlayerTag})",
        Text = dto.Text,
        WhenText = dto.CreatedAtUtc.LocalDateTime.ToString("g"),
        Initials = SocialPersonItem.MakeInitials(dto.AuthorDisplayName),
        CanDelete = dto.CanDelete
    };
}

public sealed class ChatThreadItem
{
    public int PeerUserId { get; init; }
    public required string Title { get; init; }
    public required string Preview { get; init; }
    public int UnreadCount { get; init; }
    public string UnreadBadge => UnreadCount > 0 ? UnreadCount.ToString() : string.Empty;

    public static ChatThreadItem FromDto(ChatThreadDto dto)
    {
        var title = string.IsNullOrWhiteSpace(dto.Peer.PlayerTag) ? dto.Peer.DisplayName : dto.Peer.PlayerTag!;
        return new ChatThreadItem
        {
            PeerUserId = dto.Peer.Id,
            Title = title,
            Preview = string.IsNullOrWhiteSpace(dto.LastMessagePreview)
                ? Loc.T("Brak wiadomości")
                : dto.LastMessagePreview!,
            UnreadCount = dto.UnreadCount
        };
    }

    public static ChatThreadItem FromFriend(SocialPersonItem friend) => new()
    {
        PeerUserId = friend.UserId,
        Title = friend.Title,
        Preview = Loc.T("Nowa rozmowa"),
        UnreadCount = 0
    };
}

public sealed class ChatMessageItem
{
    public long Id { get; init; }
    public required string Text { get; init; }
    public required string Meta { get; init; }
    public bool IsMine { get; init; }

    // isMine comes from the viewer: a hub push carries the sender's IsMine to both sides.
    public static ChatMessageItem FromDto(ChatMessageDto dto, bool isMine) => new()
    {
        Id = dto.Id,
        Text = dto.Text,
        Meta = isMine
            ? Loc.F("Ty · {0:t}", dto.CreatedAtUtc.LocalDateTime)
            : dto.CreatedAtUtc.LocalDateTime.ToString("t"),
        IsMine = isMine
    };
}

public sealed class SocialPersonItem
{
    private static readonly string[] AvatarPalette =
    [
        "#3D5A80", "#5C4B7A", "#2F6F5E", "#8A5A3C", "#4A6B8A", "#6B3D5A"
    ];

    public required int UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string Title { get; init; }
    public required string Initials { get; init; }
    public required string Subtitle { get; init; }
    public required string AvatarHex { get; init; }
    public required bool IsOnline { get; init; }
    public required FriendRelation FriendRelation { get; init; }
    public required bool IsFollowing { get; init; }
    public bool ShowAddFriend => FriendRelation == FriendRelation.None;
    public bool ShowAccept => FriendRelation == FriendRelation.IncomingRequest;
    public bool ShowRemoveFriend => FriendRelation is FriendRelation.Friends or FriendRelation.OutgoingRequest;
    public string RemoveFriendLabel => FriendRelation == FriendRelation.OutgoingRequest ? Loc.T("Anuluj") : Loc.T("Usuń");
    public bool ShowFollow => !IsFollowing && FriendRelation == FriendRelation.None;
    public bool ShowUnfollow => IsFollowing && FriendRelation != FriendRelation.Friends;
    public bool ShowMessage => FriendRelation == FriendRelation.Friends;

    /// <summary>Open lobby this friend hosts; set before the row is shown.</summary>
    public LobbyDto? Lobby { get; set; }

    public bool HasLobby => Lobby is not null;
    public string JoinLabel => Lobby is { } l
        ? Loc.F("Dołącz · {0} {1}/{2}", l.GameTitle ?? "SGS", l.Players, l.MaxPlayers)
        : string.Empty;
    public bool HasActions => ShowAddFriend || ShowAccept || ShowRemoveFriend;

    public bool Matches(string query) =>
        Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);

    public static SocialPersonItem FromDto(SocialUserDto user)
    {
        var isOnline = user.IsOnline
            || (user.LastSeenUtc is { } seen
                && DateTimeOffset.UtcNow - seen.ToUniversalTime() <= TimeSpan.FromMinutes(5));
        var title = string.IsNullOrWhiteSpace(user.PlayerTag) ? user.DisplayName : user.PlayerTag!;
        var relation = user.FriendRelation switch
        {
            FriendRelation.IncomingRequest => Loc.T("Zaproszenie do Ciebie"),
            FriendRelation.OutgoingRequest => Loc.T("Zaproszenie wysłane"),
            FriendRelation.Friends => isOnline ? "Online" : "Offline",
            _ => user.IsFollowing ? Loc.T("Obserwujesz") : (user.IsFollower ? Loc.T("Obserwuje Cię") : (isOnline ? "Online" : "Offline"))
        };

        return new SocialPersonItem
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Title = title,
            Initials = MakeInitials(user.DisplayName),
            Subtitle = relation,
            AvatarHex = AvatarPalette[Math.Abs(user.Id) % AvatarPalette.Length],
            IsOnline = isOnline,
            FriendRelation = user.FriendRelation,
            IsFollowing = user.IsFollowing
        };
    }

    internal static string MakeInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        return parts.Length == 1
            ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
            : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }
}
