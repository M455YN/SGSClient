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
        PageList.SelectedIndex = 0;
        RefreshUi();
    }

    private void PageList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Ctrl+klik odznacza element; zostajemy wtedy na bieżącej stronie.
        if (PageList.SelectedIndex < 0)
        {
            return;
        }

        AppearancePage.Visibility = PageList.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        GeneralPage.Visibility = PageList.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        LibraryPage.Visibility = PageList.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        DownloadsPage.Visibility = PageList.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        AccountPage.Visibility = PageList.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = PageList.SelectedIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
        PageScroller.ChangeView(null, 0, null, true);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshAccountStatus();
        ViewModel.RefreshPreferences();
        CloseToTraySwitch.IsOn = ViewModel.CloseToTray;
        AutoUpdateSwitch.IsOn = ViewModel.AutoUpdateEnabled;
        BandwidthBox.Value = ViewModel.Downloads?.DownloadMaxMbps ?? 0;
        RefreshUi();
    }

    private void CloseToTraySwitch_Toggled(object sender, RoutedEventArgs e) => ViewModel.CloseToTray = CloseToTraySwitch.IsOn;

    private void AutoUpdateSwitch_Toggled(object sender, RoutedEventArgs e) => ViewModel.AutoUpdateEnabled = AutoUpdateSwitch.IsOn;

    private void BandwidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        // Wyczyszczone pole daje NaN.
        if (ViewModel.Downloads is { } downloads)
        {
            downloads.DownloadMaxMbps = double.IsNaN(args.NewValue) ? 0 : args.NewValue;
        }
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
