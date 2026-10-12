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
    private int? _activeChatPeerId;
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

    private void OnRealtimeMessage(ChatMessageDto message)
    {
        void Apply()
        {
            if (_activeChatPeerId is int peer
                && (message.SenderUserId == peer || message.RecipientUserId == peer))
            {
                if (ChatMessages.All(m => m.Id != message.Id))
                {
                    ChatMessages.Add(ChatMessageItem.FromDto(message));
                }
            }

            _ = LoadChatThreadsAsync();
        }

        RunOnUi(Apply);
    }

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
    public ObservableCollection<ProfileCommentItem> ProfileComments { get; } = [];
    public ObservableCollection<AchievementSummaryItem> ProfileAchievements { get; } = [];
    public ObservableCollection<ChatThreadItem> ChatThreads { get; } = [];
    public ObservableCollection<ChatMessageItem> ChatMessages { get; } = [];

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
    public partial SocialPersonItem? ViewedProfile { get; set; }

    [ObservableProperty]
    public partial string ProfileHeader { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProfileSubtitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProfileCommentDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProfileStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProfileAchievementsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int? ProfileUserId { get; set; }

    [ObservableProperty]
    public partial bool CanMessageProfile { get; set; }

    [ObservableProperty]
    public partial ChatThreadItem? SelectedChatThread { get; set; }

    [ObservableProperty]
    public partial string ChatDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChatStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChatPeerTitle { get; set; } = Loc.T("Wybierz rozmowę");

    [ObservableProperty]
    public partial bool HasActiveChat { get; set; }

    public bool ShowFriendsTab => SelectedTab == "Friends" && ViewedProfile is null;
    public bool ShowRequestsTab => SelectedTab == "Requests" && ViewedProfile is null;
    public bool ShowFollowingTab => SelectedTab == "Following" && ViewedProfile is null;
    public bool ShowFollowersTab => SelectedTab == "Followers" && ViewedProfile is null;
    public bool ShowSearchTab => SelectedTab == "Search" && ViewedProfile is null;
    public bool ShowChatTab => SelectedTab == "Chat" && ViewedProfile is null;
    public bool ShowProfileView => ViewedProfile is not null;
    public bool ShowMainList => ViewedProfile is null && SelectedTab is not "Chat";
    public bool ShowEmptyFriends => ShowFriendsTab && Friends.Count == 0;
    public bool ShowEmptyFollowing => ShowFollowingTab && Following.Count == 0;
    public bool ShowEmptyFollowers => ShowFollowersTab && Followers.Count == 0;
    public bool ShowEmptySearch => ShowSearchTab && SearchResults.Count == 0 && !IsBusy;
    public bool ShowEmptyChat => ShowChatTab && ChatThreads.Count == 0 && !HasActiveChat;
    public bool ShowChatConversation => ShowChatTab && HasActiveChat;
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

    partial void OnViewedProfileChanged(SocialPersonItem? value) => NotifyLists();

    partial void OnHasActiveChatChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyChat));
        OnPropertyChanged(nameof(ShowChatConversation));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ShowEmptySearch));

    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            ViewedProfile = null;
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

            ApplyOverview(overview);
            StatusText = overview.IncomingCount > 0
                ? Loc.F("{0} oczekujących zaproszeń.", overview.IncomingCount)
                : $"{overview.FriendsCount} znajomych";
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
        ViewedProfile = null;
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
    private async Task OpenProfileAsync(SocialPersonItem? person)
    {
        if (person is null || _userApi is null)
        {
            return;
        }

        IsBusy = true;
        ProfileStatus = Loc.T("Ładowanie profilu…");
        try
        {
            var profile = await _userApi.GetPublicProfileAsync(person.UserId);
            if (profile is null)
            {
                ProfileStatus = Loc.T("Nie udało się wczytać profilu.");
                return;
            }

            ApplyPublicProfile(profile);
        }
        catch (Exception ex)
        {
            ProfileStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseProfile()
    {
        ViewedProfile = null;
        ProfileUserId = null;
        ProfileComments.Clear();
        ProfileAchievements.Clear();
        ProfileAchievementsText = string.Empty;
        ProfileCommentDraft = string.Empty;
        ProfileStatus = string.Empty;
        NotifyLists();
    }

    [RelayCommand]
    private async Task PostProfileCommentAsync()
    {
        if (_userApi is null || ProfileUserId is not int userId)
        {
            return;
        }

        var text = ProfileCommentDraft.Trim();
        if (text.Length < 2)
        {
            ProfileStatus = Loc.T("Wpisz dłuższy komentarz.");
            return;
        }

        ProfileStatus = Loc.T("Wysyłanie…");
        var (comment, error) = await _userApi.AddProfileCommentAsync(userId, text);
        if (comment is null)
        {
            ProfileStatus = error ?? Loc.T("Nie udało się dodać komentarza.");
            return;
        }

        ProfileCommentDraft = string.Empty;
        ProfileComments.Insert(0, ProfileCommentItem.FromDto(comment));
        ProfileStatus = Loc.T("Dodano podpis.");
    }

    [RelayCommand]
    private async Task DeleteProfileCommentAsync(ProfileCommentItem? comment)
    {
        if (comment is null || _userApi is null || ProfileUserId is not int userId)
        {
            return;
        }

        var (ok, error) = await _userApi.DeleteProfileCommentAsync(userId, comment.Id);
        if (!ok)
        {
            ProfileStatus = error ?? Loc.T("Nie udało się usunąć.");
            return;
        }

        ProfileComments.Remove(comment);
        ProfileStatus = Loc.T("Usunięto.");
    }

    [RelayCommand]
    private async Task MessageFromProfileAsync()
    {
        if (ViewedProfile is null)
        {
            return;
        }

        var person = ViewedProfile;
        CloseProfile();
        SelectedTab = "Chat";
        await OpenChatWithAsync(person);
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

        SelectedTab = "Chat";
        await OpenChatWithPeerIdAsync(person.UserId, person.Title);
    }

    [RelayCommand]
    private async Task SendChatMessageAsync()
    {
        if (_userApi is null || _activeChatPeerId is not int peerId)
        {
            return;
        }

        var text = ChatDraft.Trim();
        if (text.Length == 0)
        {
            return;
        }

        ChatDraft = string.Empty;
        var (message, error) = await _userApi.SendChatMessageAsync(peerId, text);
        if (message is null)
        {
            ChatStatus = error ?? Loc.T("Nie wysłano.");
            return;
        }

        ChatMessages.Add(ChatMessageItem.FromDto(message));
        ChatStatus = string.Empty;
        await LoadChatThreadsAsync();
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
        if (_userApi is null)
        {
            return;
        }

        _activeChatPeerId = peerUserId;
        HasActiveChat = true;
        ChatPeerTitle = title;
        SelectedChatThread = ChatThreads.FirstOrDefault(t => t.PeerUserId == peerUserId);
        ChatStatus = Loc.T("Ładowanie…");
        await RefreshConversationAsync(peerUserId);
        await EnsureChatRealtimeOrPollAsync();
        OnPropertyChanged(nameof(ShowEmptyChat));
        OnPropertyChanged(nameof(ShowChatConversation));
    }

    private async Task RefreshConversationAsync(int peerUserId)
    {
        if (_userApi is null)
        {
            return;
        }

        try
        {
            var convo = await _userApi.GetChatConversationAsync(peerUserId);
            ChatMessages.Clear();
            if (convo is null)
            {
                ChatStatus = Loc.T("Nie udało się wczytać rozmowy (tylko znajomi).");
                return;
            }

            ChatPeerTitle = string.IsNullOrWhiteSpace(convo.Peer.PlayerTag)
                ? convo.Peer.DisplayName
                : convo.Peer.PlayerTag!;
            foreach (var m in convo.Messages)
            {
                ChatMessages.Add(ChatMessageItem.FromDto(m));
            }

            ChatStatus = string.Empty;
        }
        catch (Exception ex)
        {
            ChatStatus = ex.Message;
        }
    }

    private async Task PollChatAsync()
    {
        if (!ShowChatTab || _activeChatPeerId is not int peerId)
        {
            return;
        }

        try
        {
            await RefreshConversationAsync(peerId);
        }
        catch
        {
            // ignore poll errors
        }
    }

    private void ApplyPublicProfile(PublicUserProfileDto profile)
    {
        ViewedProfile = SocialPersonItem.FromDto(profile.User);
        ProfileUserId = profile.User.Id;
        ProfileHeader = ViewedProfile.Title;
        ProfileSubtitle = profile.IsOwnProfile
            ? Loc.T("Twój profil — inni mogą się tu podpisać.")
            : Loc.F("{0} · ściana komentarzy", profile.User.DisplayName);
        CanMessageProfile = !profile.IsOwnProfile && profile.User.FriendRelation == FriendRelation.Friends;
        ProfileComments.Clear();
        ProfileAchievements.Clear();
        ProfileAchievementsText = string.Empty;
        foreach (var c in profile.Comments)
        {
            ProfileComments.Add(ProfileCommentItem.FromDto(c));
        }

        ProfileStatus = ProfileComments.Count == 0
            ? Loc.T("Brak podpisów — napisz coś.")
            : Loc.F("{0} podpisów", ProfileComments.Count);

        foreach (var summary in profile.Achievements)
        {
            ProfileAchievements.Add(AchievementSummaryItem.FromDto(summary));
        }

        ProfileAchievementsText = profile switch
        {
            { AchievementsHidden: true, IsOwnProfile: false } => Loc.T("Ten gracz ukrył swoje osiągnięcia."),
            { AchievementsHidden: true } => Loc.T("Ukryłeś swoje osiągnięcia — inni gracze ich nie widzą."),
            { Achievements.Count: 0 } => Loc.T("Nie odblokowano jeszcze żadnych osiągnięć."),
            _ => Loc.F(
                "Odblokowano łącznie: {0} · {1} pkt",
                profile.Achievements.Sum(a => a.Unlocked),
                profile.Achievements.Sum(a => a.PointsEarned))
        };
        NotifyLists();
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

    private void ApplyOverview(SocialOverviewDto overview)
    {
        Replace(Friends, overview.Friends);
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
        OnPropertyChanged(nameof(ShowProfileView));
        OnPropertyChanged(nameof(ShowMainList));
        OnPropertyChanged(nameof(ShowEmptyChat));
        OnPropertyChanged(nameof(ShowChatConversation));
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
    public required string AuthorLabel { get; init; }
    public required string Text { get; init; }
    public required string WhenText { get; init; }
    public bool CanDelete { get; init; }

    public static ProfileCommentItem FromDto(ProfileCommentDto dto) => new()
    {
        Id = dto.Id,
        AuthorLabel = string.IsNullOrWhiteSpace(dto.AuthorPlayerTag)
            ? dto.AuthorDisplayName
            : $"{dto.AuthorDisplayName} (@{dto.AuthorPlayerTag})",
        Text = dto.Text,
        WhenText = dto.CreatedAtUtc.LocalDateTime.ToString("g"),
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

    public static ChatMessageItem FromDto(ChatMessageDto dto) => new()
    {
        Id = dto.Id,
        Text = dto.Text,
        Meta = dto.IsMine
            ? Loc.F("Ty · {0:t}", dto.CreatedAtUtc.LocalDateTime)
            : dto.CreatedAtUtc.LocalDateTime.ToString("t"),
        IsMine = dto.IsMine
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

    private static string MakeInitials(string name)
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
