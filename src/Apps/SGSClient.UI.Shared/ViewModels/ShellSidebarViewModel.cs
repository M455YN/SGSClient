using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;
using SGSClient.Core.Models;

namespace SGSClient.UI.ViewModels;

public partial class ShellSidebarViewModel : ObservableObject
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly IGameLauncherService? _launcher;
    private readonly ILibraryDiscoveryService? _discovery;
    private readonly IAuthSession? _auth;
    private readonly ISgsApiService? _api;
    private readonly ICatalogCacheStore? _catalogCache;
    private readonly IUiDispatcher? _ui;
    private readonly string _apiBaseUrl;
    private Action<string>? _navigate;
    private Action<int>? _openDetails;
    private Action<SidebarInstalledGame>? _openProperties;

    public ShellSidebarViewModel()
        : this(null)
    {
    }

    public ShellSidebarViewModel(
        IServiceScopeFactory? scopes = null,
        IGameLauncherService? launcher = null,
        ILibraryDiscoveryService? discovery = null,
        IAuthSession? auth = null,
        IConfiguration? configuration = null,
        NotificationsViewModel? notifications = null,
        ISgsApiService? api = null,
        ICatalogCacheStore? catalogCache = null,
        IUiDispatcher? ui = null)
    {
        _scopes = scopes;
        _launcher = launcher;
        _discovery = discovery;
        _auth = auth;
        _api = api;
        _catalogCache = catalogCache;
        _ui = ui;
        InstalledGamesNotifier.Changed += () =>
        {
            if (_ui is null)
            {
                _ = LoadInstalledAsync();
            }
            else
            {
                _ui.Post(() => _ = LoadInstalledAsync());
            }
        };
        Notifications = notifications ?? new NotificationsViewModel();
        LobbyJoin.Handler = JoinLobbyAsync;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        RefreshProfile();
    }

    public NotificationsViewModel Notifications { get; }

    public void SetNavigateHandler(Action<string> navigate) => _navigate = navigate;
    public void SetOpenDetailsHandler(Action<int> open) => _openDetails = open;
    public void SetOpenPropertiesHandler(Action<SidebarInstalledGame> open) => _openProperties = open;

    public ObservableCollection<SidebarInstalledGame> InstalledGames { get; } = [];

    [ObservableProperty]
    public partial string SelectedNavTag { get; set; } = "Home";

    [ObservableProperty]
    public partial bool IsAdminVisible { get; set; }

    /// <summary>Creator panel is only for accounts with the creator role or higher.</summary>
    [ObservableProperty]
    public partial bool IsCreatorVisible { get; set; } = true;

    [ObservableProperty]
    public partial string ProfileDisplayName { get; set; } = Loc.T("Gość");

    [ObservableProperty]
    public partial string ProfileStatus { get; set; } = Loc.T("Niezalogowany");

    [ObservableProperty]
    public partial string? ProfileAvatarUrl { get; set; }

    [ObservableProperty]
    public partial bool HasAvatar { get; set; }

    [ObservableProperty]
    public partial string InstalledHeader { get; set; } = Loc.F("{0} zainstalowanych", 0);

    [ObservableProperty]
    public partial bool HasInstalledGames { get; set; }

    [ObservableProperty]
    public partial bool IsPaneOpen { get; set; } = true;

    public bool IsHomeSelected => SelectedNavTag == "Home";
    public bool IsGamesSelected => SelectedNavTag == "Games";
    public bool IsLibrarySelected => SelectedNavTag == "Library";
    public bool IsDownloadsSelected => SelectedNavTag == "Downloads";
    public bool IsWishlistSelected => SelectedNavTag == "Wishlist";
    public bool IsCreativeSelected => SelectedNavTag == "Creative";
    public bool IsPublishSelected => SelectedNavTag == "Publish";
    public bool IsStudioSelected => SelectedNavTag is "Studio" or "Publish";
    public bool IsAdminSelected => SelectedNavTag == "Admin";
    public bool IsSettingsSelected => SelectedNavTag == "Settings";
    public bool IsAccountSelected => SelectedNavTag == "Account";

    partial void OnSelectedNavTagChanged(string value)
    {
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(IsGamesSelected));
        OnPropertyChanged(nameof(IsLibrarySelected));
        OnPropertyChanged(nameof(IsDownloadsSelected));
        OnPropertyChanged(nameof(IsWishlistSelected));
        OnPropertyChanged(nameof(IsCreativeSelected));
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
            ProfileDisplayName = Loc.T("Lokalnie");
            ProfileStatus = Loc.T("Logowanie wyłączone");
            ProfileAvatarUrl = null;
            HasAvatar = false;
            IsCreatorVisible = true;
            return;
        }

        if (_auth.IsLoggedIn && _auth.CurrentUser is not null)
        {
            var user = _auth.CurrentUser;
            ProfileDisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? Loc.T("Gracz") : user.DisplayName;
            var roleSuffix = user.Role switch
            {
                UserRole.SuperAdmin => " · superadmin",
                UserRole.Admin => " · admin",
                UserRole.Creator => Loc.T(" · twórca"),
                _ => string.Empty
            };
            ProfileStatus = !string.IsNullOrWhiteSpace(user.PlayerTag)
                ? $"@{user.PlayerTag}{roleSuffix}"
                : string.IsNullOrWhiteSpace(user.Email)
                    ? $"Zalogowany{roleSuffix}"
                    : $"{user.Email}{roleSuffix}";
            var avatar = InstallPathHelper.TryResolveDownloadUrl(_apiBaseUrl, user.AvatarUrl);
            ProfileAvatarUrl = avatar is null ? null : CacheBust(avatar);
            HasAvatar = !string.IsNullOrWhiteSpace(ProfileAvatarUrl);
            IsAdminVisible = user.IsAdmin;
            IsCreatorVisible = user.CanPublish;
            return;
        }

        ProfileDisplayName = Loc.T("Gość");
        ProfileStatus = Loc.T("Zaloguj się w Profilu");
        ProfileAvatarUrl = null;
        HasAvatar = false;
        IsAdminVisible = false;
        IsCreatorVisible = false;
    }

    public void SetAdminVisible(bool visible) => IsAdminVisible = visible;

    public void SetRoleVisibility(bool admin, bool creator)
    {
        IsAdminVisible = admin;
        IsCreatorVisible = creator;
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
        _ = LoadInstalledAsync();
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
            InstalledHeader = Loc.F("{0} zainstalowanych", 0);
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
            var items = await library.GetOwnedAsync();
            await SyncArtworkAsync(items, library);

            InstalledGames.Clear();
            foreach (var item in items
                         .Where(x => x.InstallState == InstallState.Installed)
                         .OrderBy(x => x.Title))
            {
                InstalledGames.Add(SidebarInstalledGame.FromModel(item, _apiBaseUrl));
            }

            HasInstalledGames = InstalledGames.Count > 0;
            InstalledHeader = Loc.F("{0} zainstalowanych", InstalledGames.Count);
        }
        catch
        {
            // keep previous list on transient failures
        }
    }

    private async Task SyncArtworkAsync(IReadOnlyList<GameLibraryItem> items, ILocalLibraryStore store)
    {
        IReadOnlyList<SGSClient.Core.DTOs.GameDto> games = [];
        try
        {
            if (_api is not null)
            {
                games = await _api.GetGamesAsync();
            }
        }
        catch
        {
            if (_catalogCache is not null)
            {
                games = (await _catalogCache.LoadCatalogAsync()).Games;
            }
        }

        if (games.Count == 0)
        {
            return;
        }

        var byId = games.ToDictionary(g => g.Id);
        foreach (var item in items)
        {
            if (!byId.TryGetValue(item.GameId, out var game))
            {
                continue;
            }

            if (item.ApplyStoreArtwork(game.CoverUrl, game.LogoUrl, game.HeaderUrl))
            {
                await store.UpsertAsync(item);
            }
        }
    }

    /// <summary>
    /// Starts the game with the lobby code so it joins the host. When the game is not installed
    /// (or cannot start) the player lands on its page instead, to install it.
    /// </summary>
    public async Task JoinLobbyAsync(int gameId, string code)
    {
        try
        {
            if (_scopes is not null && _launcher is not null)
            {
                using var scope = _scopes.CreateScope();
                var library = scope.ServiceProvider.GetService<ILocalLibraryStore>();
                var item = library is null ? null : (await library.GetOwnedAsync()).FirstOrDefault(x => x.GameId == gameId);
                if (item is { InstallState: InstallState.Installed } && !string.IsNullOrWhiteSpace(item.ExecutablePath))
                {
                    var result = await _launcher.LaunchAsync(new GameLaunchRequest
                    {
                        GameId = gameId,
                        Title = item.Title,
                        ExecutablePath = item.ExecutablePath,
                        LobbyCode = code
                    });
                    if (result.Started)
                    {
                        await LoadInstalledAsync();
                        return;
                    }
                }
            }
        }
        catch
        {
            // fall through to the game page
        }

        _openDetails?.Invoke(gameId);
    }

    [RelayCommand]
    private async Task LaunchAsync(SidebarInstalledGame? item)
    {
        if (item is null || _launcher is null || string.IsNullOrWhiteSpace(item.ExecutablePath) || _scopes is null)
        {
            return;
        }

        try
        {
            var result = await _launcher.LaunchAsync(new GameLaunchRequest
            {
                GameId = item.GameId,
                Title = item.Title,
                ExecutablePath = item.ExecutablePath
            });
            _ = result;
            await LoadInstalledAsync();
        }
        catch
        {
        }
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
}

public sealed class SidebarInstalledGame
{
    public required int GameId { get; init; }
    public required int BuildId { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? ExecutablePath { get; init; }
    public string? CoverImageUrl { get; init; }
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImageUrl);
    public string StateText { get; init; } = string.Empty;
    public bool CanLaunch { get; init; }
    public DateTimeOffset? LastPlayedUtc { get; init; }
    public string TileLabel { get; init; } = string.Empty;

    public static SidebarInstalledGame FromModel(GameLibraryItem item, string apiBaseUrl)
    {
        var canLaunch = item.InstallState == InstallState.Installed
            && !string.IsNullOrWhiteSpace(item.ExecutablePath);
        string? cover = null;
        if (!string.IsNullOrWhiteSpace(item.CustomCoverPath) && File.Exists(item.CustomCoverPath))
        {
            cover = ToImageSourceUrl(new Uri(Path.GetFullPath(item.CustomCoverPath)).AbsoluteUri);
        }
        else
        {
            var square = !string.IsNullOrWhiteSpace(item.LogoUrl) ? item.LogoUrl : item.CoverUrl;
            cover = ToImageSourceUrl(InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, square));
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
            LastPlayedUtc = item.LastPlayedUtc,
            StateText = canLaunch ? Loc.T("Gotowa do uruchomienia") : Loc.T("Brak pliku wykonywalnego"),
            TileLabel = string.IsNullOrWhiteSpace(item.Symbol)
                ? item.Title[..Math.Min(2, item.Title.Length)].ToUpperInvariant()
                : item.Symbol[..Math.Min(3, item.Symbol.Length)].ToUpperInvariant()
        };
    }

    /// <summary>
    /// WinUI <c>x:Bind</c> to <see cref="Microsoft.UI.Xaml.Controls.Image.Source"/> throws on relative/invalid URIs.
    /// </summary>
    private static string? ToImageSourceUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https" or "file" ? uri.AbsoluteUri : null;
    }
}
