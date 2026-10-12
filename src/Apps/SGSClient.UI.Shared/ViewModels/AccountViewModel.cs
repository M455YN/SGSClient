using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Auth;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Core.Playtime;

namespace SGSClient.UI.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    private readonly IAuthSession? _auth;
    private readonly IUserApiClient? _userApi;
    private readonly IServiceScopeFactory? _scopes;
    private readonly string _apiBaseUrl;
    private Func<nint>? _parentWindowHandle;
    private Action<string>? _navigate;
    private Action? _onLogout;
    private Action<bool>? _adminVisibility;

    /// <summary>Raised after the profile (name, avatar, tag) changed so the shell can refresh its sidebar.</summary>
    public event Action? ProfileChanged;

    /// <summary>Raised when the signed-in user's admin rights are known (or cleared on sign-out).</summary>
    public event Action<bool>? AdminVisibilityChanged;

    public AccountViewModel(
        IAuthSession? auth = null,
        IUserApiClient? userApi = null,
        IServiceScopeFactory? scopes = null,
        IConfiguration? configuration = null)
    {
        _auth = auth;
        _userApi = userApi;
        _scopes = scopes;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        AccountStatus = auth is null
            ? Loc.T("Logowanie niedostępne")
            : auth.IsAvailable
                ? Loc.T("Nie zalogowano")
                : auth.LastError ?? Loc.T("Logowanie nie jest skonfigurowane.");
    }

    public ObservableCollection<RecentGameItem> RecentGames { get; } = [];
    public ObservableCollection<AchievementSummaryItem> Achievements { get; } = [];
    public bool HasAchievements => Achievements.Count > 0;

    [ObservableProperty] public partial string AchievementsTotalText { get; set; } = string.Empty;
    public ObservableCollection<ProfileCommentItem> WallComments { get; } = [];

    public bool ShowSignedOut => !IsLoggedIn;
    public bool ShowTagSetup => IsLoggedIn && NeedsPlayerTagSetup;
    public bool ShowAccountContent => IsLoggedIn && !NeedsPlayerTagSetup;
    public bool ShowProfile => IsLoggedIn && !IsEditing && !NeedsPlayerTagSetup;
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
    public bool HasRecentGames => RecentGames.Count > 0;
    public bool ShowEmptyRecentGames => IsLoggedIn && !NeedsPlayerTagSetup && RecentGames.Count == 0;
    public string BioDisplay => string.IsNullOrWhiteSpace(Bio) ? Loc.T("Dodaj bio w Dostosuj profil") : Bio;
    public string LocationDisplay => string.IsNullOrWhiteSpace(Location) ? "—" : Location;
    public string PlayerTagDisplay => string.IsNullOrWhiteSpace(PlayerTag) ? string.Empty : $"@{PlayerTag}";
    public string TagPreview => string.IsNullOrWhiteSpace(DraftPlayerTag)
        ? "@twoj.tag"
        : "@" + DraftPlayerTag.Trim().TrimStart('@').ToLowerInvariant();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSignedOut))]
    [NotifyPropertyChangedFor(nameof(ShowProfile))]
    [NotifyPropertyChangedFor(nameof(ShowTagSetup))]
    [NotifyPropertyChangedFor(nameof(ShowAccountContent))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyRecentGames))]
    public partial bool IsLoggedIn { get; set; }

    [ObservableProperty]
    public partial string AccountStatus { get; set; } = Loc.T("Nie zalogowano");

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProfile))]
    [NotifyPropertyChangedFor(nameof(ShowTagSetup))]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WallCommentDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WallStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ProfileUserId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayerTagDisplay))]
    public partial string PlayerTag { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTagSetup))]
    [NotifyPropertyChangedFor(nameof(ShowProfile))]
    [NotifyPropertyChangedFor(nameof(ShowAccountContent))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyRecentGames))]
    public partial bool NeedsPlayerTagSetup { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagPreview))]
    public partial string DraftPlayerTag { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TagStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    public partial string AvatarUrl { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BioDisplay))]
    public partial string Bio { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocationDisplay))]
    public partial string Location { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastActivityText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditDisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditBio { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditLocation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditPlayerTag { get; set; } = string.Empty;

    /// <summary>Privacy switch in the profile editor: hide achievements from other players.</summary>
    [ObservableProperty]
    public partial bool EditHideAchievements { get; set; }

    private bool _hideAchievements;

    [ObservableProperty]
    public partial int FriendsCount { get; set; }

    [ObservableProperty]
    public partial int FollowingCount { get; set; }

    [ObservableProperty]
    public partial int FollowersCount { get; set; }

    [ObservableProperty]
    public partial string AvatarInitials { get; set; } = "?";

    [ObservableProperty]
    public partial string ProfileActionStatus { get; set; } = string.Empty;

    public void SetParentWindowHandleProvider(Func<nint> provider) => _parentWindowHandle = provider;
    public void SetNavigateHandler(Action<string> navigate) => _navigate = navigate;
    public void SetLogoutHandler(Action onLogout) => _onLogout = onLogout;

    /// <summary>
    /// Convenience wrapper over <see cref="AdminVisibilityChanged"/>. Without a configured
    /// identity provider every nav entry stays visible (local development).
    /// </summary>
    public void SetAdminVisibilityHandler(Action<bool> handler)
    {
        if (_adminVisibility is not null)
        {
            AdminVisibilityChanged -= _adminVisibility;
        }

        _adminVisibility = handler;
        AdminVisibilityChanged += handler;
        if (_auth?.IsAvailable != true)
        {
            handler(true);
        }
    }

    public async Task InitializeAsync()
    {
        if (_auth is null || !_auth.IsAvailable)
        {
            AccountStatus = _auth?.LastError
                ?? Loc.T("Logowanie nie jest skonfigurowane.");
            return;
        }

        IsBusy = true;
        try
        {
            if (_auth.IsLoggedIn && _auth.CurrentUser is not null)
            {
                await ApplyProfileAsync(_auth.CurrentUser);
            }
            else if (await _auth.TrySilentLoginAsync() && _auth.CurrentUser is not null)
            {
                await ApplyProfileAsync(_auth.CurrentUser);
            }
            else
            {
                ApplySignedOut();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (_auth is null || !_auth.IsAvailable)
        {
            AccountStatus = _auth?.LastError
                ?? Loc.T("Logowanie nie jest skonfigurowane.");
            return;
        }

        IsBusy = true;
        try
        {
            var ok = await _auth.LoginAsync(_parentWindowHandle?.Invoke() ?? 0);
            if (ok && _auth.CurrentUser is not null)
            {
                await ApplyProfileAsync(_auth.CurrentUser);
            }
            else
            {
                ApplySignedOut();
                AccountStatus = _auth.LastError ?? Loc.T("Nie udało się zalogować albo logowanie anulowano.");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (_auth is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _auth.LogoutAsync();
            ApplySignedOut();
            _onLogout?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BeginEdit()
    {
        if (!IsLoggedIn)
        {
            return;
        }

        EditDisplayName = DisplayName;
        EditBio = Bio;
        EditLocation = Location;
        EditPlayerTag = PlayerTag;
        EditHideAchievements = _hideAchievements;
        ProfileActionStatus = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (_userApi is null || !IsLoggedIn)
        {
            AccountStatus = "Profile API unavailable";
            return;
        }

        IsBusy = true;
        try
        {
            var updated = await _userApi.UpdateMeAsync(new UpdateProfileRequestDto
            {
                DisplayName = EditDisplayName,
                Bio = EditBio,
                Location = EditLocation,
                PlayerTag = string.IsNullOrWhiteSpace(EditPlayerTag) ? null : EditPlayerTag,
                HideAchievements = EditHideAchievements
            });

            if (updated is null)
            {
                AccountStatus = Loc.T("Nie udało się zapisać profilu. Sprawdź połączenie z serwerem.");
                return;
            }

            _auth?.SetCurrentUser(updated);
            await ApplyProfileAsync(updated);
            IsEditing = false;
            ProfileChanged?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UploadAvatarFromPathAsync(string? localFilePath)
    {
        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
        {
            ProfileActionStatus = Loc.T("Nie można odczytać wybranego pliku.");
            return;
        }

        await using var stream = File.OpenRead(localFilePath);
        await UploadAvatarFromStreamAsync(stream, Path.GetFileName(localFilePath));
    }

    public async Task UploadAvatarFromStreamAsync(Stream content, string fileName)
    {
        if (_userApi is null || !IsLoggedIn)
        {
            ProfileActionStatus = Loc.T("Zaloguj się, aby wgrać zdjęcie.");
            return;
        }

        IsBusy = true;
        ProfileActionStatus = Loc.T("Wgrywanie zdjęcia…");
        try
        {
            var (updated, error) = await _userApi.UploadAvatarAsync(content, fileName);
            if (updated is null)
            {
                ProfileActionStatus = error ?? Loc.T("Upload nieudany.");
                return;
            }

            _auth?.SetCurrentUser(updated);
            await ApplyProfileAsync(updated);
            ProfileActionStatus = Loc.T("Zdjęcie zaktualizowane.");
            ProfileChanged?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearAvatarAsync()
    {
        if (_userApi is null || !IsLoggedIn)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var updated = await _userApi.ClearAvatarAsync();
            if (updated is null)
            {
                ProfileActionStatus = Loc.T("Nie udało się usunąć zdjęcia.");
                return;
            }

            _auth?.SetCurrentUser(updated);
            await ApplyProfileAsync(updated);
            ProfileActionStatus = Loc.T("Zdjęcie usunięte.");
            ProfileChanged?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyProfileAsync(UserProfileDto user)
    {
        IsLoggedIn = true;
        DisplayName = user.DisplayName;
        PlayerTag = user.PlayerTag ?? string.Empty;
        NeedsPlayerTagSetup = _auth?.IsAvailable == true && user.Id > 0 && user.NeedsPlayerTag;
        DraftPlayerTag = NeedsPlayerTagSetup
            ? (PlayerTagRules.SuggestFrom(user.DisplayName, user.Email) ?? string.Empty)
            : PlayerTag;
        TagStatus = NeedsPlayerTagSetup
            ? Loc.T("Tag jest unikalny w całym SGS.")
            : string.Empty;
        Email = user.Email ?? string.Empty;
        AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl)
            ? string.Empty
            : CacheBust(InstallPathHelper.ResolveDownloadUrl(_apiBaseUrl, user.AvatarUrl));
        Bio = user.Bio ?? string.Empty;
        Location = user.Location ?? string.Empty;
        _hideAchievements = user.HideAchievements;
        AvatarInitials = MakeInitials(user.DisplayName);
        LastActivityText = FormatLastActivity(user.LastLoginUtc);
        AccountStatus = string.IsNullOrWhiteSpace(user.Email)
            ? Loc.F("Zalogowano jako {0}", user.DisplayName)
            : Loc.F("Zalogowano jako {0} · {1}", user.DisplayName, user.Email);
        if (user.IsAdmin)
        {
            AccountStatus += " · admin";
        }
        FriendsCount = FollowingCount = FollowersCount = 0;
        await LoadSocialCountsAsync();
        await LoadRecentGamesAsync();
        await LoadAchievementsAsync();
        await LoadWallAsync(user.Id);
        OnPropertyChanged(nameof(HasRecentGames));
        OnPropertyChanged(nameof(ShowEmptyRecentGames));
        AdminVisibilityChanged?.Invoke(user.IsAdmin);
    }

    private async Task LoadWallAsync(int userId)
    {
        ProfileUserId = userId;
        WallComments.Clear();
        WallStatus = string.Empty;
        if (_userApi is null || userId <= 0)
        {
            return;
        }

        try
        {
            var profile = await _userApi.GetPublicProfileAsync(userId);
            if (profile is null)
            {
                WallStatus = Loc.T("Nie udało się wczytać ściany.");
                return;
            }

            foreach (var c in profile.Comments)
            {
                WallComments.Add(ProfileCommentItem.FromDto(c));
            }

            WallStatus = WallComments.Count == 0
                ? Loc.T("Brak podpisów — znajomi mogą coś tu napisać.")
                : Loc.F("{0} podpisów na ścianie", WallComments.Count);
        }
        catch (Exception ex)
        {
            WallStatus = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PostWallCommentAsync()
    {
        if (_userApi is null || ProfileUserId <= 0)
        {
            return;
        }

        var text = WallCommentDraft.Trim();
        if (text.Length < 2)
        {
            WallStatus = Loc.T("Wpisz dłuższy komentarz.");
            return;
        }

        var (comment, error) = await _userApi.AddProfileCommentAsync(ProfileUserId, text);
        if (comment is null)
        {
            WallStatus = error ?? Loc.T("Nie udało się dodać podpisu.");
            return;
        }

        WallCommentDraft = string.Empty;
        WallComments.Insert(0, ProfileCommentItem.FromDto(comment));
        WallStatus = Loc.T("Dodano podpis.");
    }

    [RelayCommand]
    private async Task DeleteWallCommentAsync(ProfileCommentItem? comment)
    {
        if (comment is null || _userApi is null || ProfileUserId <= 0)
        {
            return;
        }

        var (ok, error) = await _userApi.DeleteProfileCommentAsync(ProfileUserId, comment.Id);
        if (!ok)
        {
            WallStatus = error ?? Loc.T("Nie udało się usunąć.");
            return;
        }

        WallComments.Remove(comment);
        WallStatus = Loc.T("Usunięto.");
    }

    private void ApplySignedOut()
    {
        IsLoggedIn = false;
        IsEditing = false;
        DisplayName = Email = AvatarUrl = Bio = Location = LastActivityText = PlayerTag = string.Empty;
        NeedsPlayerTagSetup = false;
        DraftPlayerTag = TagStatus = string.Empty;
        AvatarInitials = "?";
        FriendsCount = FollowingCount = FollowersCount = 0;
        RecentGames.Clear();
        Achievements.Clear();
        AchievementsTotalText = string.Empty;
        OnPropertyChanged(nameof(HasAchievements));
        WallComments.Clear();
        WallCommentDraft = WallStatus = string.Empty;
        ProfileUserId = 0;
        AccountStatus = Loc.T("Nie zalogowano");
        OnPropertyChanged(nameof(HasRecentGames));
        OnPropertyChanged(nameof(ShowEmptyRecentGames));
        AdminVisibilityChanged?.Invoke(_auth?.IsAvailable != true);
    }

    [RelayCommand]
    private async Task ClaimPlayerTagAsync()
    {
        if (_userApi is null)
        {
            TagStatus = Loc.T("API niedostępne.");
            return;
        }

        if (!PlayerTagRules.TryNormalize(DraftPlayerTag, out var tag, out var error))
        {
            TagStatus = error;
            return;
        }

        IsBusy = true;
        try
        {
            var check = await _userApi.CheckPlayerTagAsync(tag);
            if (check is { Available: false })
            {
                TagStatus = check.Error ?? Loc.T("Ten tag jest zajęty.");
                return;
            }

            var (profile, claimError) = await _userApi.ClaimPlayerTagAsync(tag);
            if (profile is null)
            {
                TagStatus = claimError ?? Loc.T("Nie udało się zapisać tagu.");
                return;
            }

            _auth?.SetCurrentUser(profile);
            await ApplyProfileAsync(profile);
            ProfileChanged?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenFriends(string? tab) =>
        _navigate?.Invoke(string.IsNullOrWhiteSpace(tab) ? "Friends" : $"Friends:{tab}");

    private async Task LoadSocialCountsAsync()
    {
        if (_userApi is null)
        {
            return;
        }

        try
        {
            var social = await _userApi.GetSocialAsync();
            if (social is null)
            {
                return;
            }

            FriendsCount = social.FriendsCount;
            FollowingCount = social.FollowingCount;
            FollowersCount = social.FollowersCount;
        }
        catch
        {
        }
    }

    private async Task LoadAchievementsAsync()
    {
        Achievements.Clear();
        AchievementsTotalText = string.Empty;
        if (_userApi is null)
        {
            return;
        }

        try
        {
            var summaries = await _userApi.GetAchievementSummariesAsync();
            foreach (var summary in summaries)
            {
                Achievements.Add(AchievementSummaryItem.FromDto(summary));
            }

            AchievementsTotalText = summaries.Count == 0
                ? Loc.T("Nie odblokowano jeszcze żadnych osiągnięć.")
                : Loc.F("Odblokowano łącznie: {0} · {1} pkt", summaries.Sum(s => s.Unlocked), summaries.Sum(s => s.PointsEarned));
        }
        catch
        {
            // the profile still works without achievements
        }
        finally
        {
            OnPropertyChanged(nameof(HasAchievements));
        }
    }

    private async Task LoadRecentGamesAsync()
    {
        RecentGames.Clear();
        if (_scopes is null)
        {
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetService<ILocalLibraryStore>();
            var playtime = scope.ServiceProvider.GetService<IPlaytimeStore>();
            if (library is null)
            {
                return;
            }

            var items = await library.GetAllAsync();
            if (playtime is not null)
            {
                await playtime.SyncFromServerAsync();
                await playtime.ApplyToLibraryAsync(items);
            }

            foreach (var item in items
                         .OrderByDescending(x => x.LastPlayedUtc ?? DateTimeOffset.MinValue)
                         .Take(9))
            {
                RecentGames.Add(new RecentGameItem(item.Title, item.Symbol, item.LastPlayedUtc, item.TotalPlayTimeSeconds));
            }
        }
        catch
        {
        }
    }

    private static string CacheBust(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var sep = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{sep}v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
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

    private static string FormatLastActivity(DateTimeOffset? lastLoginUtc)
    {
        if (lastLoginUtc is null)
        {
            return Loc.T("Brak aktywności");
        }

        var age = DateTimeOffset.UtcNow - lastLoginUtc.Value;
        if (age.TotalMinutes < 5)
        {
            return Loc.T("Ostatnia aktywność: Online");
        }

        if (age.TotalHours < 24)
        {
            return Loc.F("Ostatnia aktywność {0} godz. temu", (int)age.TotalHours);
        }

        if (age.TotalDays < 60)
        {
            return Loc.F("Ostatnia aktywność {0} dn. temu", (int)age.TotalDays);
        }

        return Loc.F("Ostatnia aktywność {0:d}", lastLoginUtc.Value.LocalDateTime);
    }
}

public sealed class RecentGameItem(string title, string symbol, DateTimeOffset? lastPlayedUtc, long playtimeSeconds = 0)
{
    public string Title { get; } = title;
    public string TileLabel { get; } = string.IsNullOrWhiteSpace(symbol)
        ? title[..Math.Min(2, title.Length)].ToUpperInvariant()
        : symbol[..Math.Min(3, symbol.Length)].ToUpperInvariant();
    public string Subtitle { get; } = FormatSubtitle(lastPlayedUtc, playtimeSeconds);

    private static string FormatSubtitle(DateTimeOffset? lastPlayedUtc, long playtimeSeconds)
    {
        var when = lastPlayedUtc is null
            ? Loc.T("Niegrana")
            : lastPlayedUtc.Value.LocalDateTime.ToString("g");
        return playtimeSeconds > 0
            ? $"{PlaytimeFormat.ToPolishTotal(playtimeSeconds)} · {when}"
            : when;
    }
}
