using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class LibraryPage : Page
{
    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
        InitializeComponent();
        ViewModel.SetOpenPropertiesHandler(OpenProperties);
        ViewModel.SetOpenDetailsHandler(OpenDetails);
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
        RefreshUi();
    }

    private void RefreshUi()
    {
        SidebarStatus.Text = ViewModel.StatusText;
        EmptyText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        HomeScroll.Visibility = ViewModel.ShowHome && ViewModel.HasGames ? Visibility.Visible : Visibility.Collapsed;

        SidebarList.ItemsSource = ViewModel.FilteredGames;
        RecentList.ItemsSource = ViewModel.RecentGames;
        AllGamesList.ItemsSource = ViewModel.AllGames;
        RecentPanel.Visibility = ViewModel.ShowRecent ? Visibility.Visible : Visibility.Collapsed;
        AllGamesHeader.Text = ViewModel.AllGamesHeader;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectHomeCommand.Execute(null);
        RefreshUi();
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        ViewModel.SearchText = sender.Text ?? string.Empty;
        SidebarList.ItemsSource = null;
        SidebarList.ItemsSource = ViewModel.FilteredGames;
    }

    private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SidebarList.SelectedItem is LibraryGameItem item)
        {
            ViewModel.SelectGameCommand.Execute(item);
        }
    }

    private void Poster_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LibraryGameItem item)
        {
            ViewModel.SelectGameCommand.Execute(item);
        }
    }

    private void OpenDetails(int gameId)
    {
        App.MainWindow?.NavigateTo(
            typeof(GameDetailsPage),
            parameter: new GameDetailsNavArgs(gameId, "Library"),
            selectTag: "Library");
    }

    private async void OpenProperties(LibraryGameItem item)
    {
        var dialog = new GamePropertiesDialog();
        await dialog.ShowForGameAsync(item.GameId, item.BuildId, item.Title, XamlRoot);
        await ViewModel.LoadAsync();
        RefreshUi();
    }
}
