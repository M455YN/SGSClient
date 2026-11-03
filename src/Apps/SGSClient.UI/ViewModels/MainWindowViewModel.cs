using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HomeViewModel _home;
    private readonly GamesViewModel _games;
    private readonly GameDetailsViewModel _gameDetails;
    private readonly LibraryViewModel _library;
    private readonly DownloadsViewModel _downloads;
    private readonly WishlistViewModel _wishlist;
    private readonly PublishViewModel _publish;
    private readonly CreativeViewModel _creative;
    private readonly AddMediaViewModel _addMedia;
    private readonly AddStoryViewModel _addStory;
    private readonly StudioViewModel _studio;
    private readonly AdminViewModel _admin;
    private readonly SettingsViewModel _settings;
    private readonly AccountViewModel _account;
    private readonly FriendsViewModel _friends;
    private readonly LoginGateViewModel _loginGate;
    private readonly INetworkStatusService? _network;
    private string _friendsTab = "Friends";

    public MainWindowViewModel()
        : this(
            new HomeViewModel(),
            new GamesViewModel(),
            new GameDetailsViewModel(),
            new LibraryViewModel(),
            new DownloadsViewModel(),
            new WishlistViewModel(),
            new CreativeViewModel(),
            new PublishViewModel(),
            new AddMediaViewModel(null, null),
            new AddStoryViewModel(null, null),
            new StudioViewModel(),
            new AdminViewModel(),
            new SettingsViewModel(),
            new AccountViewModel(),
            new FriendsViewModel(),
            new ShellSidebarViewModel(),
            new LoginGateViewModel())
    {
    }

    public MainWindowViewModel(
        HomeViewModel home,
        GamesViewModel games,
        GameDetailsViewModel gameDetails,
        LibraryViewModel library,
        DownloadsViewModel downloads,
        WishlistViewModel wishlist,
        CreativeViewModel creative,
        PublishViewModel publish,
        AddMediaViewModel addMedia,
        AddStoryViewModel addStory,
        StudioViewModel studio,
        AdminViewModel admin,
        SettingsViewModel settings,
        AccountViewModel account,
        FriendsViewModel friends,
        ShellSidebarViewModel sidebar,
        LoginGateViewModel loginGate,
        INetworkStatusService? network = null,
        IAuthSession? auth = null)
    {
        _home = home;
        _games = games;
        _gameDetails = gameDetails;
        _library = library;
        _downloads = downloads;
        _wishlist = wishlist;
        _publish = publish;
        _creative = creative;
        _addMedia = addMedia;
        _addStory = addStory;
        _studio = studio;
        _admin = admin;
        _settings = settings;
        _account = account;
        _friends = friends;
        _loginGate = loginGate;
        _network = network;
        Sidebar = sidebar;
        CurrentPage = _home;
        SelectedNavTag = "Home";

        if (_network is not null)
        {
            void RefreshBanners()
            {
                IsOffline = !_network.IsOnline;
                IsMaintenance = _network.IsMaintenance;
                MaintenanceText = MaintenanceNotice.Text(auth);
            }

            RefreshBanners();
            _network.Changed += (_, _) => RefreshBanners();
        }

        _games.SetOpenDetailsHandler(id => OpenGameDetails(id, "Games"));
        _library.SetOpenDetailsHandler(id => OpenGameDetails(id, "Library"));
        _downloads.SetOpenDetailsHandler(id => OpenGameDetails(id, "Downloads"));
        _wishlist.SetOpenDetailsHandler(id => OpenGameDetails(id, "Wishlist"));
        _wishlist.SetOpenAccountHandler(() => Navigate("Account"));
        _gameDetails.SetCloseHandler(CloseGameDetails);
        _home.SetNavigateHandler(NavigateFromHome);
        _account.SetNavigateHandler(OpenSocial);
        _account.SetAdminVisibilityHandler(visible =>
        {
            IsAdminVisible = visible;
            Sidebar.SetRoleVisibility(visible, visible);
            Sidebar.RefreshProfile();
        });
        _account.ProfileChanged += Sidebar.RefreshProfile;
        _account.SetLogoutHandler(() =>
        {
            ShowLoginGate = _loginGate.IsAuthConfigured;
            CloseSocial();
            Sidebar.RefreshProfile();
            _ = Sidebar.Notifications.StopAsync();
        });
        _loginGate.SetOnAuthenticated(EnterAuthenticatedShellAsync);
        Sidebar.SelectNav("Home");
    }

    public AccountViewModel Account => _account;
    public GamesViewModel Games => _games;
    public GameDetailsViewModel GameDetails => _gameDetails;
    public ShellSidebarViewModel Sidebar { get; }
    public LoginGateViewModel LoginGate => _loginGate;
    public DownloadsViewModel Downloads => _downloads;
    public FriendsViewModel Friends => _friends;

    [ObservableProperty]
    private ObservableObject _currentPage;

    [ObservableProperty]
    private string _selectedNavTag = "Home";

    [ObservableProperty]
    private bool _isAdminVisible;

    [ObservableProperty]
    private bool _isSocialOpen;

    [ObservableProperty]
    private bool _showLoginGate;

    [ObservableProperty]
    private bool _isOffline;

    [ObservableProperty]
    private bool _isMaintenance;

    [ObservableProperty]
    private string _maintenanceText = string.Empty;

    private string _detailsReturnTag = "Games";
    private string _contentNavTag = "Home";

    [RelayCommand]
    private void Navigate(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (tag.StartsWith("Game:", StringComparison.Ordinal))
        {
            if (int.TryParse(tag.AsSpan(5), out var gameId))
            {
                OpenGameDetails(gameId, "Games");
            }

            return;
        }

        if (tag.StartsWith("Games:genre:", StringComparison.OrdinalIgnoreCase))
        {
            var genre = tag["Games:genre:".Length..];
            _games.ApplyGenreFilter(genre);
            _contentNavTag = "Games";
            CloseSocial();
            SelectedNavTag = "Games";
            Sidebar.SelectNav("Games");
            CurrentPage = _games;
            _ = _games.LoadAsync();
            return;
        }

        if (tag == "Friends")
        {
            if (IsSocialOpen)
            {
                CloseSocial();
            }
            else
            {
                OpenSocial();
            }

            return;
        }

        _contentNavTag = tag;
        CloseSocial();
        SelectedNavTag = tag;
        Sidebar.SelectNav(tag);
        CurrentPage = tag switch
        {
            "Games" => _games,
            "Library" => _library,
            "Downloads" => _downloads,
            "Wishlist" => _wishlist,
            "Creative" => _creative,
            "Publish" => _publish,
            "Studio" => _studio,
            "Admin" => _admin,
            "Settings" => _settings,
            "Account" => _account,
            _ => _home
        };

        if (tag == "Publish")
        {
            _publish.RefreshAuthHint();
        }

        if (tag == "Library")
        {
            _ = _library.LoadAsync();
            _ = Sidebar.LoadInstalledAsync();
        }

        if (tag == "Downloads")
        {
            _downloads.Refresh();
        }

        if (tag == "Wishlist")
        {
            _ = _wishlist.LoadAsync();
        }

        if (tag == "Creative")
        {
            _ = _creative.LoadAsync();
        }

        if (tag is "Home")
        {
            _ = Sidebar.LoadInstalledAsync();
        }

        if (tag == "Studio")
        {
            _ = _studio.LoadAsync();
        }

        if (tag == "Admin")
        {
            _ = _admin.LoadAsync();
        }

        if (tag == "Account")
        {
            Sidebar.RefreshProfile();
        }
    }

    public void OpenSocial(string? tab = null)
    {
        if (tab is { Length: > 0 } && tab.StartsWith("Friends:", StringComparison.Ordinal))
        {
            _friendsTab = tab.Length > 8 ? tab[8..] : "Friends";
        }
        else if (tab is "Following" or "Followers" or "Requests" or "Search")
        {
            _friendsTab = tab;
        }
        else if (!string.IsNullOrWhiteSpace(tab) && tab != "Friends")
        {
            Navigate(tab);
            return;
        }

        IsSocialOpen = true;
        _ = _friends.InitializeAsync(_friendsTab);
        _friendsTab = "Friends";
    }

    public void CloseSocial()
    {
        if (!IsSocialOpen)
        {
            return;
        }

        IsSocialOpen = false;
        SelectedNavTag = _contentNavTag;
        Sidebar.SelectNav(_contentNavTag);
    }

    public void NavigateToPublish(
        string? parentSymbol = null,
        string selectTag = "Studio",
        string? kind = null,
        int? versionOfGameId = null)
    {
        if (versionOfGameId is { } gameId)
        {
            _publish.PrepareForNewVersion(gameId, asAdmin: selectTag == "Admin");
        }
        else if (kind == "app")
        {
            _publish.PrepareForApp();
        }
        else if (kind == "dlc" || !string.IsNullOrWhiteSpace(parentSymbol))
        {
            _publish.PrepareForDlc(parentSymbol ?? string.Empty);
        }
        else
        {
            _publish.PrepareForNewGame();
        }

        _contentNavTag = selectTag;
        CloseSocial();
        SelectedNavTag = selectTag;
        Sidebar.SelectNav(selectTag);
        CurrentPage = _publish;
        _publish.RefreshAuthHint();
    }

    public void NavigateToAddMedia(string kind, string selectTag = "Studio")
    {
        _addMedia.PrepareFor(kind);
        ShowAddScreen(_addMedia, selectTag);
    }

    public void NavigateToAddStory(string selectTag = "Studio")
    {
        _addStory.Prepare();
        ShowAddScreen(_addStory, selectTag);
    }

    private void ShowAddScreen(ObservableObject page, string selectTag)
    {
        _contentNavTag = selectTag;
        CloseSocial();
        SelectedNavTag = selectTag;
        Sidebar.SelectNav(selectTag);
        CurrentPage = page;
    }

    private void NavigateFromHome(string tag) => Navigate(tag);

    public void OpenGameDetails(int gameId, string returnTag = "Games")
    {
        _detailsReturnTag = string.IsNullOrWhiteSpace(returnTag) ? "Games" : returnTag;
        SelectedNavTag = _detailsReturnTag switch
        {
            "Library" => "Library",
            "Downloads" => "Downloads",
            "Wishlist" => "Wishlist",
            _ => "Games"
        };
        Sidebar.SelectNav(SelectedNavTag);
        CurrentPage = _gameDetails;
        _ = _gameDetails.LoadAsync(
            gameId,
            fromLibrary: _detailsReturnTag is "Library" or "Downloads",
            returnTag: _detailsReturnTag);
    }

    public void CloseGameDetails()
    {
        var tag = _detailsReturnTag;
        SelectedNavTag = tag;
        Sidebar.SelectNav(tag);
        CurrentPage = tag switch
        {
            "Library" => _library,
            "Downloads" => _downloads,
            "Wishlist" => _wishlist,
            _ => _games
        };

        if (tag == "Library")
        {
            _ = _library.LoadAsync();
            _ = Sidebar.LoadInstalledAsync();
        }
        else if (tag == "Downloads")
        {
            _downloads.Refresh();
        }
        else if (tag == "Wishlist")
        {
            _ = _wishlist.LoadAsync();
        }
        else
        {
            _ = _games.EnsureLoadedAsync();
        }
    }

    public async Task InitializeAsync()
    {
        var entered = await _loginGate.TrySilentEnterAsync();
        if (!entered && _loginGate.IsAuthConfigured)
        {
            ShowLoginGate = true;
            return;
        }

        await EnterAuthenticatedShellAsync();
    }

    public async Task EnterAuthenticatedShellAsync()
    {
        ShowLoginGate = false;
        await _account.InitializeAsync();
        Sidebar.RefreshProfile();
        if (_account.NeedsPlayerTagSetup)
        {
            Navigate("Account");
        }

        await Sidebar.LoadInstalledAsync();
        await _home.ReloadLibraryAsync();
        await _library.LoadAsync();
        await _library.CheckUpdatesOnStartupAsync();
        await Sidebar.Notifications.StartAsync();
    }
}
