using SGSClient.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsViewModel.AccountStatus) or nameof(SettingsViewModel.StatusText)
                or nameof(SettingsViewModel.IsBusy) or nameof(SettingsViewModel.LanguageHint))
            {
                RefreshUi();
            }
        };
        TitleText.Text = ViewModel.Title;
        ThemeBox.ItemsSource = ViewModel.ThemeOptions;
        ThemeBox.SelectedIndex = ViewModel.SelectedThemeIndex;
        LanguageBox.ItemsSource = ViewModel.LanguageOptions;
        LanguageBox.SelectedIndex = ViewModel.SelectedLanguageIndex;
        StoreStatusText.Text = ViewModel.StoreStatus;
        LibrarySummaryText.Text = ViewModel.LibrarySummary;
        AboutText.Text = ViewModel.AboutText;
        RefreshUi();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshAccountStatus();
        RefreshUi();
    }

    private void RefreshUi()
    {
        AccountStatusText.Text = ViewModel.AccountStatus;
        StatusText.Text = ViewModel.StatusText;
        LanguageHintText.Text = ViewModel.LanguageHint;
        LanguageHintText.Visibility = string.IsNullOrEmpty(ViewModel.LanguageHint) ? Visibility.Collapsed : Visibility.Visible;
        LibraryFoldersList.ItemsSource = ViewModel.LibraryFolders;
        AddLibraryFolderButton.IsEnabled = !ViewModel.IsBusy;
        RemoveLibraryFolderButton.IsEnabled = !ViewModel.IsBusy;
        RescanLibraryButton.IsEnabled = !ViewModel.IsBusy;
    }

    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedIndex >= 0)
        {
            ViewModel.SelectedThemeIndex = ThemeBox.SelectedIndex;
        }
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedIndex >= 0)
        {
            ViewModel.SelectedLanguageIndex = LanguageBox.SelectedIndex;
        }
    }

    private void ExitClient_Click(object sender, RoutedEventArgs e) => App.MainWindow?.ExitApplication();

    private async void AddLibraryFolder_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var path = await WinUiFilePicker.PickFolderAsync(App.MainWindow, commitLabel: Loc.T("Dodaj folder"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ViewModel.AddLibraryFolderAsync(path);
        RefreshUi();
    }

    private async void RemoveLibraryFolder_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedLibraryFolder = LibraryFoldersList.SelectedItem as LibraryFolderDisplayItem;
        await ViewModel.RemoveSelectedLibraryFolderCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void OpenLibraryFolder_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedLibraryFolder = LibraryFoldersList.SelectedItem as LibraryFolderDisplayItem;
        ViewModel.OpenSelectedLibraryFolderCommand.Execute(null);
        RefreshUi();
    }

    private async void RescanLibrary_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RescanLibraryCommand.ExecuteAsync(null);
        RefreshUi();
    }
}
