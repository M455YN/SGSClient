using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;
using SGSClient.Core.Playtime;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.ViewModels;

/// <summary>
/// Full-page profile of another player: header, relation actions, games, achievements and comments.
/// The signed-in user's own profile is <see cref="AccountViewModel"/>.
/// </summary>
public sealed partial class UserProfileViewModel : ObservableObject
{
    private readonly IUserApiClient? _userApi;
    private readonly SocialRealtimeService? _realtime;
    private readonly IUiDispatcher? _dispatcher;
    private readonly string _apiBaseUrl;
    private Func<ChatModalViewModel, Task>? _showChat;

    public UserProfileViewModel(
        IUserApiClient? userApi = null,
        SocialRealtimeService? realtime = null,
        IUiDispatcher? dispatcher = null,
        IConfiguration? configuration = null)
    {
        _userApi = userApi;
        _realtime = realtime;
        _dispatcher = dispatcher;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        Achievements.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAchievements));
        Games.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasGames));
            OnPropertyChanged(nameof(RecentActivity));
        };
    }

    private const int ActivityCards = 4;

    /// <summary>Played store games, most recent first, with achievement progress where known.</summary>
    public ObservableCollection<RecentGameItem> Games { get; } = [];
    public bool HasGames => Games.Count > 0;
    public IReadOnlyList<RecentGameItem> RecentActivity => Games.Take(ActivityCards).ToList();

    [ObservableProperty] public partial int GamesCount { get; set; }
    [ObservableProperty] public partial string TotalPlaytimeText { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMemberSince))]
    public partial string MemberSinceText { get; set; } = string.Empty;

    public bool HasMemberSince => !string.IsNullOrEmpty(MemberSinceText);

    /// <summary>Landscape art of the most recently played game, used as the profile banner.</summary>
    [ObservableProperty] public partial string BannerImageUrl { get; set; } = string.Empty;

    /// <summary>Why the games section is empty (hidden or nothing played); empty when there are games.</summary>
    [ObservableProperty] public partial string GamesText { get; set; } = string.Empty;

    public ObservableCollection<AchievementSummaryItem> Achievements { get; } = [];
    public ObservableCollection<ProfileCommentItem> Comments { get; } = [];
    public bool HasAchievements => Achievements.Count > 0;

    /// <summary>Relation flags (ShowAddFriend, ShowMessage, …) come from the shared person item.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoaded))]
    public partial SocialPersonItem? Person { get; set; }

    public bool IsLoaded => Person is not null;

    [ObservableProperty] public partial int UserId { get; set; }
    [ObservableProperty] public partial string DisplayName { get; set; } = string.Empty;
    [ObservableProperty] public partial string PlayerTagDisplay { get; set; } = string.Empty;
    [ObservableProperty] public partial string Initials { get; set; } = "?";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    public partial string AvatarUrl { get; set; } = string.Empty;

    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);

    [ObservableProperty] public partial string Bio { get; set; } = string.Empty;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocation))]
    public partial string Location { get; set; } = string.Empty;

    public bool HasLocation => !string.IsNullOrWhiteSpace(Location);
    [ObservableProperty] public partial bool IsOnline { get; set; }
    [ObservableProperty] public partial string PresenceText { get; set; } = string.Empty;
    [ObservableProperty] public partial int AchievementsUnlocked { get; set; }
    [ObservableProperty] public partial int AchievementPoints { get; set; }
    [ObservableProperty] public partial string AchievementsText { get; set; } = string.Empty;
    [ObservableProperty] public partial string CommentDraft { get; set; } = string.Empty;
    [ObservableProperty] public partial string CommentStatus { get; set; } = string.Empty;

    /// <summary>Load or action failure shown at the top of the page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorText { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    [ObservableProperty] public partial bool IsBusy { get; set; }

    /// <summary>The view shows the conversation in a dialog and completes when it is closed.</summary>
    public void SetShowChatHandler(Func<ChatModalViewModel, Task> show) => _showChat = show;

    public async Task LoadAsync(int userId)
    {
        UserId = userId;
        ErrorText = string.Empty;
        if (_userApi is null || userId <= 0)
        {
            ErrorText = Loc.T("Nie udało się wczytać profilu.");
            return;
        }

        try
        {
            var profile = await _userApi.GetPublicProfileAsync(userId);
            if (profile is null)
            {
                ErrorText = Loc.T("Nie udało się wczytać profilu.");
                return;
            }

            Apply(profile);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    private void Apply(PublicUserProfileDto profile)
    {
        var user = profile.User;
        Person = SocialPersonItem.FromDto(user);
        UserId = user.Id;
        DisplayName = user.DisplayName;
        PlayerTagDisplay = string.IsNullOrWhiteSpace(user.PlayerTag) ? string.Empty : "@" + user.PlayerTag;
        Initials = Person.Initials;
        AvatarUrl = InstallPathHelper.TryResolveDownloadUrl(_apiBaseUrl, user.AvatarUrl) ?? string.Empty;
        Bio = user.Bio ?? string.Empty;
        Location = user.Location ?? string.Empty;
        IsOnline = Person.IsOnline;
        var lastSeen = user.LastSeenUtc ?? user.LastLoginUtc;
        PresenceText = IsOnline
            ? "Online"
            : lastSeen is { } seen
                ? Loc.F("Ostatnio online {0}", seen.LocalDateTime.ToString("d MMM yyyy", Loc.Culture))
                : "Offline";

        Achievements.Clear();
        foreach (var summary in profile.Achievements.OrderByDescending(a => a.Total == 0 ? 0 : (double)a.Unlocked / a.Total))
        {
            Achievements.Add(AchievementSummaryItem.FromDto(summary));
        }

        AchievementsUnlocked = profile.Achievements.Sum(a => a.Unlocked);
        AchievementPoints = profile.Achievements.Sum(a => a.PointsEarned);
        AchievementsText = profile switch
        {
            { AchievementsHidden: true } => Loc.T("Ten gracz ukrył swoje osiągnięcia."),
            { Achievements.Count: 0 } => Loc.T("Nie odblokowano jeszcze żadnych osiągnięć."),
            _ => Loc.F("Odblokowano łącznie: {0} · {1} pkt", AchievementsUnlocked, AchievementPoints)
        };

        MemberSinceText = profile.MemberSinceUtc == default
            ? string.Empty
            : Loc.F("Na SGS od {0}", profile.MemberSinceUtc.LocalDateTime.ToString("MMMM yyyy", Loc.Culture));
        GamesCount = profile.GamesCount;
        TotalPlaytimeText = PlaytimeFormat.ToPolishOrDash(profile.TotalPlaytimeSeconds);
        var progress = Achievements.ToDictionary(a => a.GameId);
        Games.Clear();
        foreach (var game in profile.Games)
        {
            Games.Add(new RecentGameItem(
                game.Title,
                symbol: string.Empty,
                game.LastPlayedUtc,
                game.TotalSeconds,
                game.GameId,
                InstallPathHelper.TryResolveDownloadUrl(_apiBaseUrl, game.LogoUrl ?? game.CoverUrl),
                InstallPathHelper.TryResolveDownloadUrl(_apiBaseUrl, game.HeaderUrl ?? game.CoverUrl),
                progress.GetValueOrDefault(game.GameId)));
        }

        BannerImageUrl = Games.FirstOrDefault(g => g.HasHeader)?.HeaderImageUrl ?? string.Empty;
        GamesText = profile switch
        {
            { GameActivityHidden: true } => Loc.T("Ten gracz ukrył swoje gry i czas gry."),
            { Games.Count: 0 } => Loc.T("Ten gracz nie grał jeszcze w żadną grę."),
            _ => string.Empty
        };

        Comments.Clear();
        foreach (var comment in profile.Comments)
        {
            Comments.Add(ProfileCommentItem.FromDto(comment));
        }

        CommentStatus = Comments.Count == 0 ? Loc.T("Nie ma jeszcze komentarzy.") : string.Empty;
    }

    [RelayCommand]
    private async Task PostCommentAsync()
    {
        if (_userApi is null || UserId <= 0)
        {
            return;
        }

        var text = CommentDraft.Trim();
        if (text.Length < 2)
        {
            CommentStatus = Loc.T("Wpisz dłuższy komentarz.");
            return;
        }

        var (comment, error) = await _userApi.AddProfileCommentAsync(UserId, text);
        if (comment is null)
        {
            CommentStatus = error ?? Loc.T("Nie udało się dodać komentarza.");
            return;
        }

        CommentDraft = string.Empty;
        Comments.Insert(0, ProfileCommentItem.FromDto(comment));
        CommentStatus = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteCommentAsync(ProfileCommentItem? comment)
    {
        if (comment is null || _userApi is null || UserId <= 0)
        {
            return;
        }

        var (ok, error) = await _userApi.DeleteProfileCommentAsync(UserId, comment.Id);
        if (!ok)
        {
            CommentStatus = error ?? Loc.T("Nie udało się usunąć.");
            return;
        }

        Comments.Remove(comment);
        CommentStatus = Comments.Count == 0 ? Loc.T("Nie ma jeszcze komentarzy.") : string.Empty;
    }

    [RelayCommand]
    private async Task MessageAsync()
    {
        if (_userApi is null || _showChat is null || Person is null)
        {
            return;
        }

        using var chat = new ChatModalViewModel(_userApi, _realtime, _dispatcher, UserId, Person.Title);
        _ = chat.LoadAsync();
        await _showChat(chat);
        await chat.MarkLiveMessagesReadAsync();
    }

    [RelayCommand]
    private Task AddFriendAsync() => MutateAsync(id => _userApi!.SendFriendRequestAsync(id));

    [RelayCommand]
    private Task AcceptFriendAsync() => MutateAsync(id => _userApi!.AcceptFriendRequestAsync(id));

    [RelayCommand]
    private Task RemoveFriendAsync() => MutateAsync(id => _userApi!.RemoveFriendAsync(id));

    [RelayCommand]
    private Task FollowAsync() => MutateAsync(id => _userApi!.FollowAsync(id));

    [RelayCommand]
    private Task UnfollowAsync() => MutateAsync(id => _userApi!.UnfollowAsync(id));

    /// <summary>Runs a relation change, then reloads the profile so the buttons match the new state.</summary>
    private async Task MutateAsync(Func<int, Task<(SocialOverviewDto? Overview, string? Error)>> action)
    {
        if (_userApi is null || UserId <= 0 || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (overview, error) = await action(UserId);
            if (overview is null)
            {
                ErrorText = error ?? Loc.T("Nie udało się zapisać.");
                return;
            }

            await LoadAsync(UserId);
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
