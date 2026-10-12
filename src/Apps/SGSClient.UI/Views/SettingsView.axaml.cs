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
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.RefreshAccountStatus();
            }
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.RefreshAccountStatus();
            }
        };
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
