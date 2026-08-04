using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    private readonly IAuthSession? _auth;
    private readonly IUserApiClient? _userApi;
    private readonly IServiceScopeFactory? _scopes;
    private Func<nint>? _parentWindowHandle;

    public AccountViewModel(IAuthSession? auth, IUserApiClient? userApi = null, IServiceScopeFactory? scopes = null)
    {
        _auth = auth;
        _userApi = userApi;
        _scopes = scopes;
        AccountStatus = auth is null
            ? "Auth not registered"
            : auth.IsAvailable
                ? "Not signed in"
                : auth.LastError ?? "Azure AD B2C is disabled in appsettings.";
    }

    public ObservableCollection<RecentGameItem> RecentGames { get; } = [];

    public bool ShowSignedOut => !IsLoggedIn;
    public bool ShowProfile => IsLoggedIn && !IsEditing;
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
    public bool HasRecentGames => RecentGames.Count > 0;
    public bool ShowEmptyRecentGames => IsLoggedIn && RecentGames.Count == 0;
    public string BioDisplay => string.IsNullOrWhiteSpace(Bio) ? "Dodaj bio w Dostosuj profil" : Bio;
    public string LocationDisplay => string.IsNullOrWhiteSpace(Location) ? "—" : Location;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSignedOut))]
    [NotifyPropertyChangedFor(nameof(ShowProfile))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyRecentGames))]
    public partial bool IsLoggedIn { get; set; }

    [ObservableProperty]
    public partial string AccountStatus { get; set; } = "Not signed in";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProfile))]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

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
    public partial int FriendsCount { get; set; }

    [ObservableProperty]
    public partial int FollowingCount { get; set; }

    [ObservableProperty]
    public partial int FollowersCount { get; set; }

    [ObservableProperty]
    public partial string AvatarInitials { get; set; } = "?";

    public void SetParentWindowHandleProvider(Func<nint> provider) => _parentWindowHandle = provider;

    public async Task InitializeAsync()
    {
        if (_auth is null || !_auth.IsAvailable)
        {
            AccountStatus = _auth?.LastError
                ?? "Azure AD B2C is disabled. Set AzureAd:Enabled, ClientId and TenantName in appsettings.json (UI + Api).";
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
                ?? "Azure AD B2C is disabled. Set AzureAd:Enabled, ClientId and TenantName in appsettings.json (UI + Api).";
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
                AccountStatus = _auth.LastError ?? "Sign-in failed or cancelled";
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
                Location = EditLocation
            });

            if (updated is null)
            {
                AccountStatus = "Could not save profile. Is the API running?";
                return;
            }

            _auth?.SetCurrentUser(updated);
            await ApplyProfileAsync(updated);
            IsEditing = false;
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
        Email = user.Email ?? string.Empty;
        AvatarUrl = user.AvatarUrl ?? string.Empty;
        Bio = user.Bio ?? string.Empty;
        Location = user.Location ?? string.Empty;
        AvatarInitials = MakeInitials(user.DisplayName);
        LastActivityText = FormatLastActivity(user.LastLoginUtc);
        AccountStatus = $"Signed in as {user.DisplayName}";
        FriendsCount = FollowingCount = FollowersCount = 0;
        await LoadRecentGamesAsync();
        OnPropertyChanged(nameof(HasRecentGames));
        OnPropertyChanged(nameof(ShowEmptyRecentGames));
    }

    private void ApplySignedOut()
    {
        IsLoggedIn = false;
        IsEditing = false;
        DisplayName = Email = AvatarUrl = Bio = Location = LastActivityText = string.Empty;
        AvatarInitials = "?";
        FriendsCount = FollowingCount = FollowersCount = 0;
        RecentGames.Clear();
        AccountStatus = "Not signed in";
        OnPropertyChanged(nameof(HasRecentGames));
        OnPropertyChanged(nameof(ShowEmptyRecentGames));
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
            if (library is null)
            {
                return;
            }

            foreach (var item in (await library.GetAllAsync())
                         .OrderByDescending(x => x.LastPlayedUtc ?? DateTimeOffset.MinValue)
                         .Take(9))
            {
                RecentGames.Add(new RecentGameItem(item.Title, item.Symbol, item.LastPlayedUtc));
            }
        }
        catch
        {
        }
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
            return "Brak aktywności";
        }

        var age = DateTimeOffset.UtcNow - lastLoginUtc.Value;
        if (age.TotalMinutes < 5)
        {
            return "Ostatnia aktywność: Online";
        }

        if (age.TotalHours < 24)
        {
            return $"Ostatnia aktywność {(int)age.TotalHours} godz. temu";
        }

        if (age.TotalDays < 60)
        {
            return $"Ostatnia aktywność {(int)age.TotalDays} dn. temu";
        }

        return $"Ostatnia aktywność {lastLoginUtc.Value.LocalDateTime:d}";
    }
}

public sealed class RecentGameItem(string title, string symbol, DateTimeOffset? lastPlayedUtc)
{
    public string Title { get; } = title;
    public string TileLabel { get; } = string.IsNullOrWhiteSpace(symbol)
        ? title[..Math.Min(2, title.Length)].ToUpperInvariant()
        : symbol[..Math.Min(3, symbol.Length)].ToUpperInvariant();
    public string Subtitle { get; } = lastPlayedUtc is null
        ? "Niegrana"
        : lastPlayedUtc.Value.LocalDateTime.ToString("g");
}
