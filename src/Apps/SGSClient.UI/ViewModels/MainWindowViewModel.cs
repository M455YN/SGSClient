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
    private readonly SettingsViewModel _settings;
    private readonly AccountViewModel _account;

    public MainWindowViewModel()
        : this(
            new HomeViewModel(),
            new GamesViewModel(),
            new GameDetailsViewModel(),
            new LibraryViewModel(),
            new PublishViewModel(),
            new SettingsViewModel(),
            new AccountViewModel())
    {
    }

    public MainWindowViewModel(
        HomeViewModel home,
        GamesViewModel games,
        GameDetailsViewModel gameDetails,
        LibraryViewModel library,
        PublishViewModel publish,
        SettingsViewModel settings,
        AccountViewModel account)
    {
        _home = home;
        _games = games;
        _gameDetails = gameDetails;
        _library = library;
        _publish = publish;
        _settings = settings;
        _account = account;
        CurrentPage = _home;
        SelectedNavTag = "Home";

        _games.SetOpenDetailsHandler(OpenGameDetails);
        _gameDetails.SetCloseHandler(CloseGameDetails);
        _home.SetNavigateHandler(NavigateFromHome);
    }

    public AccountViewModel Account => _account;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _selectedNavTag = "Home";

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
                OpenGameDetails(gameId);
            }

            return;
        }

        SelectedNavTag = tag;
        CurrentPage = tag switch
        {
            "Games" => _games,
            "Library" => _library,
            "Publish" => _publish,
            "Settings" => _settings,
            "Account" => _account,
            _ => _home
        };

        if (tag == "Publish")
        {
            _publish.RefreshAuthHint();
        }
    }

    private void NavigateFromHome(string tag) => Navigate(tag);

    public void OpenGameDetails(int gameId)
    {
        SelectedNavTag = "Games";
        CurrentPage = _gameDetails;
        _ = _gameDetails.LoadAsync(gameId);
    }

    public void CloseGameDetails()
    {
        SelectedNavTag = "Games";
        CurrentPage = _games;
        _ = _games.EnsureLoadedAsync();
    }

    public async Task InitializeAsync()
    {
        await _account.InitializeAsync();
    }
}
