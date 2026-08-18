using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        ViewModel = App.Services.GetRequiredService<HomeViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.NavigateRequested += OnNavigateRequested;
        ViewModel.OpenGameRequested += OnOpenGameRequested;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HomeViewModel.CanPublish) or null)
            {
                ApplyPublishColumn();
            }
        };
        ApplyPublishColumn();
    }

    private void ApplyPublishColumn() =>
        PublishColumn.Width = ViewModel.CanPublish ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.EnsureLoadedAsync();
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenStoreCommand.Execute(null);

    private void OpenLibrary_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenLibraryCommand.Execute(null);

    private void OpenPublish_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenPublishCommand.Execute(null);

    private void OpenFriends_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenFriendsCommand.Execute(null);

    private void OpenHero_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenHeroCommand.Execute(null);

    private void Rail_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is HomeGameCard item)
        {
            ViewModel.OpenGameCommand.Execute(item);
        }
    }

    private void Category_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is HomeCategoryItem item)
        {
            ViewModel.OpenCategoryCommand.Execute(item);
        }
    }

    private void OnNavigateRequested(string tag)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        if (tag == "Friends")
        {
            App.MainWindow.OpenSocialOverlay();
            return;
        }

        var pageType = tag switch
        {
            "Games" => typeof(GamesPage),
            "Library" => typeof(LibraryPage),
            "Publish" => typeof(PublishPage),
            _ => typeof(HomePage)
        };

        App.MainWindow.NavigateTo(pageType, selectTag: tag);
    }

    private void OnOpenGameRequested(int gameId)
    {
        App.MainWindow?.NavigateTo(typeof(GameDetailsPage), parameter: new GameDetailsNavArgs(gameId, "Games"), selectTag: "Games");
    }
}
