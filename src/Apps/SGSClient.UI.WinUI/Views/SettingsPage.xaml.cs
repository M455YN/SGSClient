using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.WinUI.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        TitleText.Text = ViewModel.Title;
        AppearanceText.Text = ViewModel.AppearanceText;
        LanguageText.Text = ViewModel.LanguageText;
        AccountStatusText.Text = ViewModel.AccountStatus;
        StoreStatusText.Text = ViewModel.StoreStatus;
        LibrarySummaryText.Text = ViewModel.LibrarySummary;
        AboutText.Text = ViewModel.AboutText;
        RefreshUi();
    }

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;
        LibraryFoldersList.ItemsSource = ViewModel.LibraryFolders;
        AddLibraryFolderButton.IsEnabled = !ViewModel.IsBusy;
        RemoveLibraryFolderButton.IsEnabled = !ViewModel.IsBusy;
        RescanLibraryButton.IsEnabled = !ViewModel.IsBusy;
    }

    private async void AddLibraryFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            await ViewModel.AddLibraryFolderAsync(folder.Path);
            RefreshUi();
        }
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
