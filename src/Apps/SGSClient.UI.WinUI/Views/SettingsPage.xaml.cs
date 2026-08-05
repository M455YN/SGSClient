using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshStatus();
        TitleText.Text = ViewModel.Title;
        AppearanceText.Text = ViewModel.AppearanceText;
        LanguageText.Text = ViewModel.LanguageText;
        AccountStatusText.Text = ViewModel.AccountStatus;
        StoreStatusText.Text = ViewModel.StoreStatus;
        LibrarySummaryText.Text = ViewModel.LibrarySummary;
        AboutText.Text = ViewModel.AboutText;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        StatusText.Text = ViewModel.StatusText;
        ClearLibraryButton.IsEnabled = !ViewModel.IsBusy;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenDataFolderCommand.Execute(null);

    private async void ClearLibrary_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearLibraryCommand.ExecuteAsync(null);
        RefreshStatus();
    }
}
