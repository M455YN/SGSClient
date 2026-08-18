using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class FriendsViewModel : ObservableObject
{
    private readonly IUserApiClient? _userApi;
    private readonly IAuthSession? _auth;

    public FriendsViewModel(IUserApiClient? userApi = null, IAuthSession? auth = null)
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
    public string RequestsTabLabel => IncomingCount > 0 ? $"Zaproszenia ({IncomingCount})" : "Zaproszenia";

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(ShowFriendsTab));
        OnPropertyChanged(nameof(ShowRequestsTab));
        OnPropertyChanged(nameof(ShowFollowingTab));
        OnPropertyChanged(nameof(ShowFollowersTab));
        OnPropertyChanged(nameof(ShowSearchTab));
        NotifyEmpty();
    }

    public async Task InitializeAsync(string? tab = null)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            SelectedTab = tab;
        }

        await LoadAsync();
    }

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
            NotifyEmpty();
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
            if (ShowSearchTab && SearchResults.Count > 0)
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
        OnPropertyChanged(nameof(RequestsTabLabel));
        OnPropertyChanged(nameof(HasIncoming));
        OnPropertyChanged(nameof(HasOutgoing));
        NotifyEmpty();
    }

    private static void Replace(ObservableCollection<SocialPersonItem> target, IReadOnlyList<SocialUserDto> source)
    {
        target.Clear();
        foreach (var user in source)
        {
            target.Add(SocialPersonItem.FromDto(user));
        }
    }

    private void NotifyEmpty()
    {
        OnPropertyChanged(nameof(ShowEmptyFriends));
        OnPropertyChanged(nameof(ShowEmptyFollowing));
        OnPropertyChanged(nameof(ShowEmptyFollowers));
        OnPropertyChanged(nameof(ShowEmptySearch));
        OnPropertyChanged(nameof(HasIncoming));
        OnPropertyChanged(nameof(HasOutgoing));
    }
}

public sealed class SocialPersonItem
{
    public required int UserId { get; init; }
    public required string DisplayName { get; init; }
    public required string Initials { get; init; }
    public required string Subtitle { get; init; }
    public required FriendRelation FriendRelation { get; init; }
    public required bool IsFollowing { get; init; }
    public bool ShowAddFriend => FriendRelation == FriendRelation.None;
    public bool ShowAccept => FriendRelation == FriendRelation.IncomingRequest;
    public bool ShowRemoveFriend => FriendRelation is FriendRelation.Friends or FriendRelation.OutgoingRequest;
    public string RemoveFriendLabel => FriendRelation == FriendRelation.OutgoingRequest ? "Anuluj" : "Usuń";
    public bool ShowFollow => !IsFollowing;
    public bool ShowUnfollow => IsFollowing;

    public static SocialPersonItem FromDto(SocialUserDto user)
    {
        var location = string.IsNullOrWhiteSpace(user.Location) ? null : user.Location.Trim();
        var relation = user.FriendRelation switch
        {
            FriendRelation.Friends => "Znajomy",
            FriendRelation.IncomingRequest => "Zaproszenie do Ciebie",
            FriendRelation.OutgoingRequest => "Zaproszenie wysłane",
            _ => user.IsFollowing ? "Obserwujesz" : (user.IsFollower ? "Obserwuje Cię" : null)
        };
        var tag = string.IsNullOrWhiteSpace(user.PlayerTag) ? null : $"@{user.PlayerTag}";
        var subtitle = string.Join(" · ", new[] { tag, location, relation }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new SocialPersonItem
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Initials = MakeInitials(user.DisplayName),
            Subtitle = string.IsNullOrWhiteSpace(subtitle) ? "Gracz SGS" : subtitle,
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
