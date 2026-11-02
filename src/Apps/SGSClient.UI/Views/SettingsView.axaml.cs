using SGSClient.Core.Localization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        // Subskrypcja po InitializeComponent: AlwaysSelected wybiera pierwszy element już przy ładowaniu XAML.
        PageList.SelectionChanged += (_, _) => ShowPage(PageList.SelectedIndex);
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.RefreshAccountStatus();
                vm.RefreshPreferences();
            }
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.RefreshAccountStatus();
                vm.RefreshPreferences();
            }
        };
    }

    private void ShowPage(int index)
    {
        AppearancePage.IsVisible = index == 0;
        GeneralPage.IsVisible = index == 1;
        LibraryPage.IsVisible = index == 2;
        DownloadsPage.IsVisible = index == 3;
        AccountPage.IsVisible = index == 4;
        AboutPage.IsVisible = index == 5;
        PageScroller.Offset = default;
    }

    private void ExitClient_OnClick(object? sender, RoutedEventArgs e) => App.ExitApplication();

    private async void AddLibraryFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is not { } storage)
        {
            vm.StatusText = Loc.T("Wybór folderu niedostępny na tej platformie.");
            return;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.T("Dodaj folder biblioteki gier"),
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            await vm.AddLibraryFolderAsync(path);
        }
    }

    private async void RemoveLibraryFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            await vm.RemoveSelectedLibraryFolderCommand.ExecuteAsync(null);
        }
    }

    private void OpenLibraryFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.OpenSelectedLibraryFolderCommand.Execute(null);
        }
    }

    private async void RescanLibrary_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            await vm.RescanLibraryCommand.ExecuteAsync(null);
        }
    }
}
