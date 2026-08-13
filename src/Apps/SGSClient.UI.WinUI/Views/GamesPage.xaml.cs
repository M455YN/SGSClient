using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.WinUI.ViewModels;
using Windows.UI;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class GamesPage : Page
{
    private static readonly SolidColorBrush CardBrush = new(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush CardHoverBrush = new(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush CardBorderBrush = new(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush CardBorderHoverBrush = new(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF));

    public GamesViewModel ViewModel { get; }

    public GamesPage()
    {
        ViewModel = App.Services.GetRequiredService<GamesViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        TitleText.Text = ViewModel.Title;
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
        StatusText.Text = ViewModel.StatusText;
        EmptyText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        GamesGrid.Visibility = ViewModel.HasGames ? Visibility.Visible : Visibility.Collapsed;
        GamesGrid.ItemsSource = ViewModel.Games;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void AddToLibrary_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameListItem item)
        {
            await ViewModel.AddToLibraryCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private void GamesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is GameListItem item)
        {
            Frame.Navigate(typeof(GameDetailsPage), new GameDetailsNavArgs(item.Id, "Games"));
        }
    }

    private void GameCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = CardHoverBrush;
            border.BorderBrush = CardBorderHoverBrush;
        }
    }

    private void GameCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = CardBrush;
            border.BorderBrush = CardBorderBrush;
        }
    }
}
