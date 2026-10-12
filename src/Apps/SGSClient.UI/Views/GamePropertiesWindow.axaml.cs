using SGSClient.Core.Localization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class GamePropertiesWindow : Window
{
    private bool _uninstallArmed;
    private bool _moveArmed;

    public GamePropertiesViewModel ViewModel { get; }

    public GamePropertiesWindow()
    {
        ViewModel = App.Services.GetRequiredService<GamePropertiesViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    public async void SetGame(int gameId, int buildId, string title)
    {
        Title = Loc.F("Właściwości — {0}", title);
        await ViewModel.LoadAsync(gameId, buildId, title);
    }

    private void NavList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: PropertiesNavItem item })
        {
            ViewModel.SelectSectionCommand.Execute(item.Id);
        }
    }

    private async void PickCustomCover_OnClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("Wybierz obrazek kafla"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("Obrazy"))
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
                }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ViewModel.ApplyCustomCoverFromFileAsync(path);
    }

    private async void ClearCustomCover_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.ClearCustomCoverCommand.ExecuteAsync(null);
    }

    private async void DlcInstallCheck_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || box.Tag is not DlcRowItem row)
        {
            return;
        }

        var wantInstalled = box.IsChecked == true;
        await ViewModel.SetDlcInstalledAsync(row, wantInstalled);
        box.IsChecked = row.IsInstalled;
    }

    private void BrowseLocalFiles_OnClick(object? sender, RoutedEventArgs e)
    {
        ViewModel.BrowseLocalFilesCommand.Execute(null);
    }

    private async void RefreshInstallInfo_OnClick(object? sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshInstallInfoCommand.ExecuteAsync(null);
    }

    private async void UninstallFiles_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (!_uninstallArmed)
        {
            _uninstallArmed = true;
            button.Content = Loc.T("Potwierdź odinstalowanie");
            ViewModel.StatusText = Loc.T("Kliknij ponownie, aby usunąć lokalne pliki.");
            return;
        }

        _uninstallArmed = false;
        button.Content = Loc.T("Odinstaluj…");
        await ViewModel.UninstallFilesCommand.ExecuteAsync(null);
        if (!ViewModel.CanUninstallFiles)
        {
            button.Content = Loc.T("Odinstaluj…");
        }
    }

    private async void MoveFiles_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (!_moveArmed)
        {
            _moveArmed = true;
            button.Content = Loc.T("Potwierdź przeniesienie");
            ViewModel.StatusText = Loc.T("Kliknij ponownie, aby przenieść pliki do wybranego folderu.");
            return;
        }

        _moveArmed = false;
        button.Content = Loc.T("Przenieś pliki…");
        await ViewModel.MoveFilesCommand.ExecuteAsync(null);
        button.Content = Loc.T("Przenieś pliki…");
    }
}
