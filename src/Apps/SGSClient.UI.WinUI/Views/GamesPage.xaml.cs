using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class GamesPage : Page
{
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
}
