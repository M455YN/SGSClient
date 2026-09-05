using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class WishlistPage : Page
{
    public WishlistViewModel ViewModel { get; }

    public WishlistPage()
    {
        ViewModel = App.Services.GetRequiredService<WishlistViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        TitleText.Text = ViewModel.Title;
        RefreshUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
        RefreshUi();
    }

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;
        EmptyText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        GamesGrid.Visibility = ViewModel.HasGames ? Visibility.Visible : Visibility.Collapsed;
        GamesGrid.ItemsSource = ViewModel.Games;
        LoginButton.Visibility = ViewModel.NeedsLogin ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void Login_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenAccountCommand.Execute(null);

    private async void AddToLibrary_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameListItem item)
        {
            await ViewModel.AddToLibraryCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameListItem item)
        {
            await ViewModel.RemoveCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private void GamesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GameListItem item)
        {
            Frame.Navigate(typeof(GameDetailsPage), new GameDetailsNavArgs(item.Id, "Wishlist"));
        }
    }
}
