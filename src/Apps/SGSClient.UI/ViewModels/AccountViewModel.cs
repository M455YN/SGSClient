using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.ViewModels;

public partial class AccountViewModel : ViewModelBase
{
    private readonly IAuthSession? _auth;
    private readonly IUserApiClient? _userApi;
    private readonly IServiceScopeFactory? _scopes;
    private readonly string _apiBaseUrl;
    private Func<nint>? _parentWindowHandle;
    private Action<bool>? _adminVisibility;

    public AccountViewModel()
        : this(null, null, null, null)
    {
    }

    public AccountViewModel(
        IAuthSession? auth,
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
            ? "Auth not registered"
            : auth.IsAvailable
                ? "Not signed in"
                : auth.LastError ?? "Azure AD B2C is disabled in appsettings.";
    }

    public void SetAdminVisibilityHandler(Action<bool> handler)
    {
        _adminVisibility = handler;
        if (_auth?.IsAvailable != true)
        {
            _adminVisibility(true);
        }
    }

    public ObservableCollection<RecentGameItem> RecentGames { get; } = [];

    public bool ShowSignedOut => !IsLoggedIn;
    public bool ShowProfile => IsLoggedIn && !IsEditing;
    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);
    public bool ShowAvatarImage => AvatarBitmap is not null;
    public bool HasRecentGames => RecentGames.Count > 0;
    public bool ShowEmptyRecentGames => IsLoggedIn && RecentGames.Count == 0;
    public string BioDisplay => string.IsNullOrWhiteSpace(Bio) ? "Dodaj bio w Dostosuj profil" : Bio;
    public string LocationDisplay => string.IsNullOrWhiteSpace(Location) ? "—" : Location;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSignedOut))]
    [NotifyPropertyChangedFor(nameof(ShowProfile))]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _accountStatus = "Not signed in";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowProfile))]
    private bool _isEditing;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvatar))]
    [NotifyPropertyChangedFor(nameof(ShowAvatarImage))]
    private string _avatarUrl = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAvatarImage))]
    private Avalonia.Media.Imaging.Bitmap? _avatarBitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BioDisplay))]
    private string _bio = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LocationDisplay))]
    private string _location = string.Empty;

    [ObservableProperty]
    private string _lastActivityText = string.Empty;

    [ObservableProperty]
    private string _editDisplayName = string.Empty;

    [ObservableProperty]
    private string _editBio = string.Empty;

    [ObservableProperty]
    private string _editLocation = string.Empty;

    [ObservableProperty]
    private int _friendsCount;

    [ObservableProperty]
    private int _followingCount;

    [ObservableProperty]
    private int _followersCount;

    [ObservableProperty]
    private string _avatarInitials = "?";

    [ObservableProperty]
    private string _profileActionStatus = string.Empty;

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
            else
            {
                var ok = await _auth.TrySilentLoginAsync();
                if (ok && _auth.CurrentUser is not null)
                {
                    await ApplyProfileAsync(_auth.CurrentUser);
                }
                else
                {
                    ApplySignedOut();
                }
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
            var hwnd = _parentWindowHandle?.Invoke() ?? 0;
            var ok = await _auth.LoginAsync(hwnd);
            if (ok && _auth.CurrentUser is not null)
            {
                await ApplyProfileAsync(_auth.CurrentUser);
                if (!string.IsNullOrWhiteSpace(_auth.LastError))
                {
                    AccountStatus = $"{AccountStatus} {_auth.LastError}";
                }
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

    public async Task UploadAvatarFromPathAsync(string? localFilePath)
    {
        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
        {
            ProfileActionStatus = "Nie można odczytać wybranego pliku.";
            return;
        }

        await using var stream = File.OpenRead(localFilePath);
        await UploadAvatarFromStreamAsync(stream, Path.GetFileName(localFilePath));
    }

    public async Task UploadAvatarFromStreamAsync(Stream content, string fileName)
    {
        if (_userApi is null || !IsLoggedIn)
        {
            ProfileActionStatus = "Zaloguj się, aby wgrać zdjęcie.";
            return;
        }

        IsBusy = true;
        ProfileActionStatus = "Wgrywanie zdjęcia…";
        try
        {
            var updated = await _userApi.UploadAvatarAsync(content, fileName);
            if (updated is null)
            {
                ProfileActionStatus = "Upload nieudany. Sprawdź format (png/jpg/webp/gif, max 5 MB) i czy API ma najnowszy deploy.";
                return;
            }

            _auth?.SetCurrentUser(updated);
            await ApplyProfileAsync(updated);
            ProfileActionStatus = "Zdjęcie zaktualizowane.";
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
                ProfileActionStatus = "Nie udało się usunąć zdjęcia.";
                return;
            }

            _auth?.SetCurrentUser(updated);
            await ApplyProfileAsync(updated);
            ProfileActionStatus = "Zdjęcie usunięte.";
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
        AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl)
            ? string.Empty
            : CacheBust(InstallPathHelper.ResolveDownloadUrl(_apiBaseUrl, user.AvatarUrl));
        _ = LoadAvatarBitmapAsync(AvatarUrl);
        Bio = user.Bio ?? string.Empty;
        Location = user.Location ?? string.Empty;
        AvatarInitials = MakeInitials(user.DisplayName);
        LastActivityText = FormatLastActivity(user.LastLoginUtc);
        AccountStatus = string.IsNullOrWhiteSpace(user.Email)
            ? $"Signed in as {user.DisplayName}"
            : $"Signed in as {user.DisplayName} · {user.Email}";
        if (user.IsAdmin)
        {
            AccountStatus += " · admin";
        }
        FriendsCount = 0;
        FollowingCount = 0;
        FollowersCount = 0;
        await LoadRecentGamesAsync();
        OnPropertyChanged(nameof(HasRecentGames));
        OnPropertyChanged(nameof(ShowEmptyRecentGames));
        _adminVisibility?.Invoke(user.IsAdmin);
    }

    private void ApplySignedOut()
    {
        IsLoggedIn = false;
        IsEditing = false;
        DisplayName = string.Empty;
        Email = string.Empty;
        AvatarUrl = string.Empty;
        AvatarBitmap = null;
        Bio = string.Empty;
        Location = string.Empty;
        LastActivityText = string.Empty;
        AvatarInitials = "?";
        FriendsCount = 0;
        FollowingCount = 0;
        FollowersCount = 0;
        RecentGames.Clear();
        AccountStatus = "Not signed in";
        OnPropertyChanged(nameof(HasRecentGames));
        OnPropertyChanged(nameof(ShowEmptyRecentGames));
        _adminVisibility?.Invoke(_auth?.IsAvailable != true);
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

    private async Task LoadAvatarBitmapAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            AvatarBitmap = null;
            return;
        }

        try
        {
            if (File.Exists(url))
            {
                AvatarBitmap = new Avalonia.Media.Imaging.Bitmap(url);
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            await using var stream = await http.GetStreamAsync(url);
            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            AvatarBitmap = new Avalonia.Media.Imaging.Bitmap(memory);
        }
        catch
        {
            AvatarBitmap = null;
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
            if (library is null)
            {
                return;
            }

            var items = await library.GetAllAsync();
            foreach (var item in items
                         .OrderByDescending(x => x.LastPlayedUtc ?? DateTimeOffset.MinValue)
                         .Take(9))
            {
                RecentGames.Add(new RecentGameItem(item.Title, item.Symbol, item.LastPlayedUtc));
            }
        }
        catch
        {
            // Library optional for profile chrome
        }
    }

    private static string MakeInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        if (parts.Length == 1)
        {
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        }

        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
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
    public string Symbol { get; } = symbol;
    public string TileLabel { get; } = string.IsNullOrWhiteSpace(symbol)
        ? title[..Math.Min(2, title.Length)].ToUpperInvariant()
        : symbol[..Math.Min(3, symbol.Length)].ToUpperInvariant();
    public string Subtitle { get; } = lastPlayedUtc is null
        ? "Niegrana"
        : lastPlayedUtc.Value.LocalDateTime.ToString("g");
}
