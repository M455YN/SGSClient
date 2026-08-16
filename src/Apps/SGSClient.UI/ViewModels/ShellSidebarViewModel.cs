using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;

namespace SGSClient.UI.ViewModels;

public partial class ShellSidebarViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly IGameLauncherService? _launcher;
    private readonly ILibraryDiscoveryService? _discovery;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl;
    private Action<string>? _navigate;
    private Action<int>? _openDetails;
    private Action<SidebarInstalledGame>? _openProperties;

    public ShellSidebarViewModel()
        : this(null, null, null, null, null)
    {
    }

    public ShellSidebarViewModel(
        IServiceScopeFactory? scopes,
        IGameLauncherService? launcher,
        ILibraryDiscoveryService? discovery,
        IAuthSession? auth,
        IConfiguration? configuration)
    {
        _scopes = scopes;
        _launcher = launcher;
        _discovery = discovery;
        _auth = auth;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        RefreshProfile();
    }

    public void SetNavigateHandler(Action<string> navigate) => _navigate = navigate;
    public void SetOpenDetailsHandler(Action<int> open) => _openDetails = open;
    public void SetOpenPropertiesHandler(Action<SidebarInstalledGame> open) => _openProperties = open;

    public ObservableCollection<SidebarInstalledGame> InstalledGames { get; } = [];

    [ObservableProperty]
    private string _selectedNavTag = "Home";

    [ObservableProperty]
    private bool _isAdminVisible;

    [ObservableProperty]
    private string _profileDisplayName = "Gość";

    [ObservableProperty]
    private string _profileStatus = "Niezalogowany";

    [ObservableProperty]
    private string? _profileAvatarUrl;

    [ObservableProperty]
    private Bitmap? _profileAvatarBitmap;

    [ObservableProperty]
    private bool _hasAvatar;

    [ObservableProperty]
    private string _installedHeader = "0 zainstalowanych";

    [ObservableProperty]
    private bool _hasInstalledGames;

    [ObservableProperty]
    private bool _isPaneOpen = true;

    public bool IsHomeSelected => SelectedNavTag == "Home";
    public bool IsGamesSelected => SelectedNavTag == "Games";
    public bool IsLibrarySelected => SelectedNavTag == "Library";
    public bool IsPublishSelected => SelectedNavTag == "Publish";
    public bool IsStudioSelected => SelectedNavTag == "Studio";
    public bool IsAdminSelected => SelectedNavTag == "Admin";
    public bool IsSettingsSelected => SelectedNavTag == "Settings";
    public bool IsAccountSelected => SelectedNavTag == "Account";

    partial void OnSelectedNavTagChanged(string value)
    {
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsGamesSelected));
        OnPropertyChanged(nameof(IsLibrarySelected));
        OnPropertyChanged(nameof(IsPublishSelected));
        OnPropertyChanged(nameof(IsStudioSelected));
        OnPropertyChanged(nameof(IsAdminSelected));
        OnPropertyChanged(nameof(IsSettingsSelected));
        OnPropertyChanged(nameof(IsAccountSelected));
    }

    public void RefreshProfile()
    {
        if (_auth?.IsAvailable != true)
        {
            ProfileDisplayName = "Lokalnie";
            ProfileStatus = "Logowanie wyłączone";
            ProfileAvatarUrl = null;
            ProfileAvatarBitmap = null;
            HasAvatar = false;
            return;
        }

        if (_auth.IsLoggedIn && _auth.CurrentUser is not null)
        {
            var user = _auth.CurrentUser;
            ProfileDisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? "Gracz" : user.DisplayName;
            ProfileStatus = string.IsNullOrWhiteSpace(user.Email)
                ? (user.IsAdmin ? "Zalogowany · admin" : "Zalogowany")
                : (user.IsAdmin ? $"{user.Email} · admin" : user.Email!);
            ProfileAvatarUrl = string.IsNullOrWhiteSpace(user.AvatarUrl)
                ? null
                : InstallPathHelper.ResolveDownloadUrl(_apiBaseUrl, user.AvatarUrl);
            IsAdminVisible = user.IsAdmin;
            _ = LoadAvatarAsync(ProfileAvatarUrl);
            return;
        }

        ProfileDisplayName = "Gość";
        ProfileStatus = "Zaloguj się w Profilu";
        ProfileAvatarUrl = null;
        ProfileAvatarBitmap = null;
        HasAvatar = false;
        IsAdminVisible = false;
    }

    public void SetAdminVisible(bool visible) => IsAdminVisible = visible;

    public void SelectNav(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            SelectedNavTag = tag;
        }
    }

    [RelayCommand]
    private void Navigate(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        SelectedNavTag = tag;
        _navigate?.Invoke(tag);
        if (tag is "Library" or "Home")
        {
            _ = LoadInstalledAsync();
        }
    }

    [RelayCommand]
    private void OpenProfile() => Navigate("Account");

    [RelayCommand]
    private void TogglePane() => IsPaneOpen = !IsPaneOpen;

    [RelayCommand]
    public async Task LoadInstalledAsync()
    {
        if (_scopes is null)
        {
            InstalledGames.Clear();
            HasInstalledGames = false;
            InstalledHeader = "0 zainstalowanych";
            return;
        }

        try
        {
            if (_discovery is not null)
            {
                using var scanScope = _scopes.CreateScope();
                var scanner = scanScope.ServiceProvider.GetRequiredService<ILibraryDiscoveryService>();
                await scanner.ScanAsync();
            }

            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var items = await library.GetAllAsync();

            InstalledGames.Clear();
            foreach (var item in items
                         .Where(x => x.InstallState == InstallState.Installed)
                         .OrderBy(x => x.Title))
            {
                var row = SidebarInstalledGame.FromModel(item, _apiBaseUrl);
                InstalledGames.Add(row);
                _ = row.LoadCoverAsync();
            }

            HasInstalledGames = InstalledGames.Count > 0;
            InstalledHeader = $"{InstalledGames.Count} zainstalowanych";
        }
        catch
        {
            // keep previous list
        }
    }

    [RelayCommand]
    private async Task LaunchAsync(SidebarInstalledGame? item)
    {
        if (item is null || _launcher is null || string.IsNullOrWhiteSpace(item.ExecutablePath) || _scopes is null)
        {
            return;
        }

        await _launcher.LaunchAsync(item.ExecutablePath);

        using var scope = _scopes.CreateScope();
        var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
        var existing = await library.GetByGameIdAsync(item.GameId);
        if (existing is null)
        {
            return;
        }

        existing.LastPlayedUtc = DateTimeOffset.UtcNow;
        await library.UpsertAsync(existing);
        await LoadInstalledAsync();
    }

    [RelayCommand]
    private void OpenDetails(SidebarInstalledGame? item)
    {
        if (item is not null)
        {
            _openDetails?.Invoke(item.GameId);
        }
    }

    [RelayCommand]
    private void OpenProperties(SidebarInstalledGame? item)
    {
        if (item is not null)
        {
            _openProperties?.Invoke(item);
        }
    }

    private async Task LoadAvatarAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            ProfileAvatarBitmap = null;
            HasAvatar = false;
            return;
        }

        try
        {
            if (File.Exists(url))
            {
                ProfileAvatarBitmap = new Bitmap(url);
                HasAvatar = true;
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            await using var stream = await http.GetStreamAsync(url);
            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            ProfileAvatarBitmap = new Bitmap(memory);
            HasAvatar = true;
        }
        catch
        {
            ProfileAvatarBitmap = null;
            HasAvatar = false;
        }
    }
}

public partial class SidebarInstalledGame : ObservableObject
{
    public required int GameId { get; init; }
    public required int BuildId { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? ExecutablePath { get; init; }
    public string? CoverImageUrl { get; init; }
    public string StateText { get; init; } = string.Empty;
    public bool CanLaunch { get; init; }
    public string TileLabel { get; init; } = string.Empty;

    [ObservableProperty]
    private Bitmap? _coverBitmap;

    [ObservableProperty]
    private bool _hasCover;

    public static SidebarInstalledGame FromModel(GameLibraryItem item, string apiBaseUrl)
    {
        var canLaunch = item.InstallState == InstallState.Installed
            && !string.IsNullOrWhiteSpace(item.ExecutablePath);
        string? cover = null;
        if (!string.IsNullOrWhiteSpace(item.CustomCoverPath) && File.Exists(item.CustomCoverPath))
        {
            cover = item.CustomCoverPath;
        }
        else if (!string.IsNullOrWhiteSpace(item.CoverUrl))
        {
            cover = InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, item.CoverUrl);
        }

        return new SidebarInstalledGame
        {
            GameId = item.GameId,
            BuildId = item.BuildId,
            Title = item.Title,
            Symbol = item.Symbol,
            ExecutablePath = item.ExecutablePath,
            CoverImageUrl = cover,
            CanLaunch = canLaunch,
            StateText = canLaunch ? "Gotowa do uruchomienia" : "Brak pliku wykonywalnego",
            TileLabel = string.IsNullOrWhiteSpace(item.Symbol)
                ? item.Title[..Math.Min(2, item.Title.Length)].ToUpperInvariant()
                : item.Symbol[..Math.Min(3, item.Symbol.Length)].ToUpperInvariant()
        };
    }

    public async Task LoadCoverAsync()
    {
        var url = CoverImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            CoverBitmap = null;
            HasCover = false;
            return;
        }

        try
        {
            if (File.Exists(url))
            {
                CoverBitmap = new Bitmap(url);
                HasCover = true;
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            await using var stream = await http.GetStreamAsync(url);
            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            CoverBitmap = new Bitmap(memory);
            HasCover = true;
        }
        catch
        {
            CoverBitmap = null;
            HasCover = false;
        }
    }
}
