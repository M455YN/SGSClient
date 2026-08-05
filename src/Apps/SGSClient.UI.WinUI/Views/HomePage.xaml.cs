using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        ViewModel.NavigateRequested += OnNavigateRequested;
        ViewModel.OpenGameRequested += OnOpenGameRequested;
        BrandText.Text = ViewModel.Brand;
        HeadlineText.Text = ViewModel.Headline;
        TaglineText.Text = ViewModel.Tagline;
        RefreshUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.EnsureLoadedAsync();
        RefreshUi();
    }

    private void RefreshUi()
    {
        FeaturedStatusText.Text = ViewModel.FeaturedStatus;
        FeaturedList.ItemsSource = ViewModel.HasFeatured ? ViewModel.Featured : null;
        FeaturedList.Visibility = ViewModel.HasFeatured ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenStoreCommand.Execute(null);

    private void OpenLibrary_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenLibraryCommand.Execute(null);

    private void Featured_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is HomeFeaturedItem item)
        {
            ViewModel.OpenFeaturedCommand.Execute(item);
        }
    }

    private void OnNavigateRequested(string tag)
    {
        if (App.MainWindow is null)
        {
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
        App.MainWindow?.NavigateTo(typeof(GameDetailsPage), parameter: gameId, selectTag: "Games");
    }
}
