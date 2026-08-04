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
        GamesList.Visibility = ViewModel.HasGames ? Visibility.Visible : Visibility.Collapsed;
        GamesList.ItemsSource = ViewModel.Games;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LibraryGameItem item)
        {
            await ViewModel.LaunchCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LibraryGameItem item)
        {
            await ViewModel.RemoveCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }
}
