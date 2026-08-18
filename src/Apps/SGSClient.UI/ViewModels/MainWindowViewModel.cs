using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SGSClient.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HomeViewModel _home;
    private readonly GamesViewModel _games;
    private readonly GameDetailsViewModel _gameDetails;
    private readonly LibraryViewModel _library;
    private readonly PublishViewModel _publish;
    private readonly StudioViewModel _studio;
    private readonly AdminViewModel _admin;
    private readonly SettingsViewModel _settings;
    private readonly AccountViewModel _account;
    private readonly FriendsViewModel _friends;
    private string _friendsTab = "Friends";

    public MainWindowViewModel()
        : this(
            new HomeViewModel(),
            new GamesViewModel(),
            new GameDetailsViewModel(),
            new LibraryViewModel(),
            new PublishViewModel(),
            new StudioViewModel(),
            new AdminViewModel(),
            new SettingsViewModel(),
            new AccountViewModel(),
            new FriendsViewModel(),
            new ShellSidebarViewModel())
    {
    }

    public MainWindowViewModel(
        HomeViewModel home,
        GamesViewModel games,
        GameDetailsViewModel gameDetails,
        LibraryViewModel library,
        PublishViewModel publish,
        StudioViewModel studio,
        AdminViewModel admin,
        SettingsViewModel settings,
        AccountViewModel account,
        FriendsViewModel friends,
        ShellSidebarViewModel sidebar)
    {
        _home = home;
        _games = games;
        _gameDetails = gameDetails;
        _library = library;
        _publish = publish;
        _studio = studio;
        _admin = admin;
        _settings = settings;
        _account = account;
        _friends = friends;
        Sidebar = sidebar;
        CurrentPage = _home;
        SelectedNavTag = "Home";

        _games.SetOpenDetailsHandler(id => OpenGameDetails(id, "Games"));
        _library.SetOpenDetailsHandler(id => OpenGameDetails(id, "Library"));
        _gameDetails.SetCloseHandler(CloseGameDetails);
        _home.SetNavigateHandler(NavigateFromHome);
        _account.SetNavigateHandler(OpenSocial);
        _account.SetAdminVisibilityHandler(visible =>
        {
            IsAdminVisible = visible;
            Sidebar.SetRoleVisibility(visible, visible);
            Sidebar.RefreshProfile();
        });
        Sidebar.SelectNav("Home");
    }

    public AccountViewModel Account => _account;
    public ShellSidebarViewModel Sidebar { get; }

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _selectedNavTag = "Home";

    [ObservableProperty]
    private bool _isAdminVisible;

    [ObservableProperty]
    private bool _isSocialOpen;

    public FriendsViewModel Friends => _friends;

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

    public void NavigateToPublish(string? parentSymbol = null)
    {
        if (!string.IsNullOrWhiteSpace(parentSymbol))
        {
            _publish.PrepareForDlc(parentSymbol);
        }
        else
        {
            _publish.PrepareForNewGame();
        }

        Navigate("Publish");
    }

    private void NavigateFromHome(string tag) => Navigate(tag);

    public void OpenGameDetails(int gameId, string returnTag = "Games")
    {
        _detailsReturnTag = string.IsNullOrWhiteSpace(returnTag) ? "Games" : returnTag;
        SelectedNavTag = _detailsReturnTag == "Library" ? "Library" : "Games";
        Sidebar.SelectNav(SelectedNavTag);
        CurrentPage = _gameDetails;
        _ = _gameDetails.LoadAsync(gameId, fromLibrary: _detailsReturnTag == "Library");
    }

    public void CloseGameDetails()
    {
        var tag = _detailsReturnTag;
        SelectedNavTag = tag;
        Sidebar.SelectNav(tag);
        CurrentPage = tag switch
        {
            "Library" => _library,
            _ => _games
        };

        if (tag == "Library")
        {
            _ = _library.LoadAsync();
            _ = Sidebar.LoadInstalledAsync();
        }
        else
        {
            _ = _games.EnsureLoadedAsync();
        }
    }

    public async Task InitializeAsync()
    {
        await _account.InitializeAsync();
        Sidebar.RefreshProfile();
        if (_account.NeedsPlayerTagSetup)
        {
            Navigate("Account");
        }
        await Sidebar.LoadInstalledAsync();
    }
}
