using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SGSClient.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly HomeViewModel _home;
    private readonly GamesViewModel _games;
    private readonly LibraryViewModel _library;
    private readonly SettingsViewModel _settings;
    private readonly AccountViewModel _account;

    public MainWindowViewModel()
        : this(
            new HomeViewModel(),
            new GamesViewModel(),
            new LibraryViewModel(),
            new SettingsViewModel(),
            new AccountViewModel())
    {
    }

    public MainWindowViewModel(
        HomeViewModel home,
        GamesViewModel games,
        LibraryViewModel library,
        SettingsViewModel settings,
        AccountViewModel account)
    {
        _home = home;
        _games = games;
        _library = library;
        _settings = settings;
        _account = account;
        CurrentPage = _home;
        SelectedNavTag = "Home";
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

        SelectedNavTag = tag;
        CurrentPage = tag switch
        {
            "Games" => _games,
            "Library" => _library,
            "Settings" => _settings,
            "Account" => _account,
            _ => _home
        };
    }

    public async Task InitializeAsync()
    {
        await _account.InitializeAsync();
    }
}
