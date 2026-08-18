using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.ViewModels;

public partial class FriendsViewModel : ViewModelBase
{
    private readonly IUserApiClient? _userApi;
    private readonly IAuthSession? _auth;

    public FriendsViewModel()
        : this(null, null)
    {
    }

    public FriendsViewModel(IUserApiClient? userApi, IAuthSession? auth)
    {
        _userApi = userApi;
        _auth = auth;
        StatusText = auth?.IsAvailable != true
            ? "Zaloguj się, żeby zobaczyć znajomych."
            : "Ładowanie…";
    }

    public ObservableCollection<SocialPersonItem> Friends { get; } = [];
    public ObservableCollection<SocialPersonItem> Incoming { get; } = [];
    public ObservableCollection<SocialPersonItem> Outgoing { get; } = [];
    public ObservableCollection<SocialPersonItem> Following { get; } = [];
    public ObservableCollection<SocialPersonItem> Followers { get; } = [];
    public ObservableCollection<SocialPersonItem> SearchResults { get; } = [];
    public ObservableCollection<SocialPersonItem> OnlineFriends { get; } = [];
    public ObservableCollection<SocialPersonItem> OfflineFriends { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFriendsTab))]
    [NotifyPropertyChangedFor(nameof(ShowRequestsTab))]
    [NotifyPropertyChangedFor(nameof(ShowFollowingTab))]
    [NotifyPropertyChangedFor(nameof(ShowFollowersTab))]
    [NotifyPropertyChangedFor(nameof(ShowSearchTab))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyFriends))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyFollowing))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyFollowers))]
    [NotifyPropertyChangedFor(nameof(ShowEmptySearch))]
    [NotifyPropertyChangedFor(nameof(ShowFriendsGroups))]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholder))]
    private string _selectedTab = "Friends";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptySearch))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequestsTabLabel))]
    private int _incomingCount;

    [ObservableProperty]
    private bool _onlineExpanded = true;

    [ObservableProperty]
    private bool _offlineExpanded = true;

    [ObservableProperty]
    private bool _incomingExpanded = true;

    public bool ShowFriendsTab => SelectedTab == "Friends";
    public bool ShowRequestsTab => SelectedTab == "Requests";
    public bool ShowFollowingTab => SelectedTab == "Following";
    public bool ShowFollowersTab => SelectedTab == "Followers";
    public bool ShowSearchTab => SelectedTab == "Search";
    public bool ShowEmptyFriends => ShowFriendsTab && Friends.Count == 0;
    public bool ShowEmptyFollowing => ShowFollowingTab && Following.Count == 0;
    public bool ShowEmptyFollowers => ShowFollowersTab && Followers.Count == 0;
    public bool ShowEmptySearch => ShowSearchTab && SearchResults.Count == 0 && !IsBusy;
    public bool HasIncoming => Incoming.Count > 0;
    public bool HasOutgoing => Outgoing.Count > 0;
    public bool ShowFriendsGroups => ShowFriendsTab && Friends.Count > 0;
    public int OnlineCount => OnlineFriends.Count;
    public int OfflineCount => OfflineFriends.Count;
    public string RequestsTabLabel => IncomingCount > 0 ? $"Zaproszenia ({IncomingCount})" : "Zaproszenia";
    public string SearchPlaceholder => ShowSearchTab ? "Szukaj graczy…" : "Szukaj znajomych…";

    public async Task InitializeAsync(string? tab = null)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            SelectedTab = tab;
        }

        await LoadAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (ShowFriendsTab)
        {
            RebuildPresenceLists();
        }
    }

    partial void OnSelectedTabChanged(string value) => RebuildPresenceLists();

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
            StatusText = "Zaloguj się w Profilu, żeby dodać znajomych.";
            return;
        }

        IsBusy = true;
        try
        {
            var overview = await _userApi.GetSocialAsync();
            if (overview is null)
            {
                StatusText = "Nie udało się pobrać znajomych. Zaloguj się ponownie.";
                return;
            }

            ApplyOverview(overview);
            StatusText = overview.IncomingCount > 0
                ? $"{overview.IncomingCount} oczekujących zaproszeń."
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
        SelectedTab = "Search";
        if (_userApi is null)
        {
            StatusText = "Brak klienta API.";
            return;
        }

        var q = SearchQuery.Trim();
        if (q.Length < 2)
        {
            StatusText = "Wpisz co najmniej 2 znaki.";
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
                ? "Brak graczy o tej nazwie."
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
                StatusText = error ?? "Nie udało się zapisać.";
                return;
            }

            ApplyOverview(overview);
            if (ShowSearchTab && !string.IsNullOrWhiteSpace(SearchQuery))
            {
                await SearchAsync();
                return;
            }

            StatusText = "Zapisano.";
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
        OnPropertyChanged(nameof(HasIncoming));
        OnPropertyChanged(nameof(HasOutgoing));
        OnPropertyChanged(nameof(RequestsTabLabel));
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(OfflineCount));
    }
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
    public IBrush AvatarBrush => new SolidColorBrush(Color.Parse(AvatarHex));
    public required bool IsOnline { get; init; }
    public required FriendRelation FriendRelation { get; init; }
    public required bool IsFollowing { get; init; }
    public bool ShowAddFriend => FriendRelation == FriendRelation.None;
    public bool ShowAccept => FriendRelation == FriendRelation.IncomingRequest;
    public bool ShowRemoveFriend => FriendRelation is FriendRelation.Friends or FriendRelation.OutgoingRequest;
    public string RemoveFriendLabel => FriendRelation == FriendRelation.OutgoingRequest ? "Anuluj" : "Usuń";
    public bool ShowFollow => !IsFollowing && FriendRelation == FriendRelation.None;
    public bool ShowUnfollow => IsFollowing && FriendRelation != FriendRelation.Friends;
    public bool HasActions => ShowAddFriend || ShowAccept || ShowRemoveFriend;

    public bool Matches(string query) =>
        Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase);

    public static SocialPersonItem FromDto(SocialUserDto user)
    {
        var isOnline = user.LastLoginUtc is { } at
            && DateTimeOffset.UtcNow - at.ToUniversalTime() <= TimeSpan.FromMinutes(20);
        var title = string.IsNullOrWhiteSpace(user.PlayerTag) ? user.DisplayName : user.PlayerTag!;
        var relation = user.FriendRelation switch
        {
            FriendRelation.IncomingRequest => "Zaproszenie do Ciebie",
            FriendRelation.OutgoingRequest => "Zaproszenie wysłane",
            FriendRelation.Friends => isOnline ? "Online" : "Offline",
            _ => user.IsFollowing ? "Obserwujesz" : (user.IsFollower ? "Obserwuje Cię" : (isOnline ? "Online" : "Offline"))
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
