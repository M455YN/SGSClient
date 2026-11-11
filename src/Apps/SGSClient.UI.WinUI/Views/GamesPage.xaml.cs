using SGSClient.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;
using Windows.UI;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class GamesPage : Page, ILoadingPage
{
    private static readonly SolidColorBrush CardBrush = new(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush CardHoverBrush = new(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush CardBorderBrush = new(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush CardBorderHoverBrush = new(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));

    public GamesViewModel ViewModel { get; }

    public GamesPage()
    {
        ViewModel = App.Services.GetRequiredService<GamesViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        TitleText.Text = ViewModel.Title;
        SortBox.ItemsSource = ViewModel.SortOptions;
        SortBox.SelectedItem = ViewModel.SelectedSort;
        SearchBox.Text = ViewModel.SearchText;
        GamesGrid.ItemsSource = ViewModel.Games;
        GamesList.ItemsSource = ViewModel.Games;
        RefreshUi();
    }

    public Task Ready { get; private set; } = Task.CompletedTask;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Ready = LoadForNavigationAsync(e);
    }

    private async Task LoadForNavigationAsync(NavigationEventArgs e)
    {
        SearchBox.Text = ViewModel.SearchText;
        SortBox.SelectedItem = ViewModel.SelectedSort;
        await ViewModel.EnsureLoadedAsync();
        RefreshUi();
    }

    private void RefreshUi()
    {
        StatusText.Text = string.IsNullOrEmpty(ViewModel.CountText) ? ViewModel.StatusText : ViewModel.CountText;
        FilterButtonText.Text = ViewModel.ActiveFilterCount > 0 ? $"Filtry ({ViewModel.ActiveFilterCount})" : Loc.T("Filtry");
        EmptyText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        GamesGrid.Visibility = ViewModel.ShowGrid ? Visibility.Visible : Visibility.Collapsed;
        GamesList.Visibility = ViewModel.ShowList ? Visibility.Visible : Visibility.Collapsed;
        ViewModeIcon.Glyph = ViewModel.IsListView ? "\uE80A" : "\uE8FD";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void Filter_Changed(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchText = SearchBox.Text ?? string.Empty;
    }

    private void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SortBox.SelectedItem is string sort)
        {
            ViewModel.SelectedSort = sort;
        }
    }

    private void ViewMode_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ToggleViewModeCommand.Execute(null);

    private void ToggleFilters_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ToggleFilterPanelCommand.Execute(null);

    private void ClearFilters_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ClearFiltersCommand.Execute(null);

    private async void AddToLibrary_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameListItem item)
        {
            await ViewModel.AddToLibraryCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void Wishlist_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is GameListItem item)
        {
            await ViewModel.ToggleWishlistCommand.ExecuteAsync(item);
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
