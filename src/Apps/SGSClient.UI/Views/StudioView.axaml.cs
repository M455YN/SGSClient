using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class StudioView : UserControl
{
    private static readonly FilePickerFileType ImageFiles = new("Obrazy")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"],
        MimeTypes = ["image/*"]
    };

    private static readonly FilePickerFileType VideoFiles = new("Wideo")
    {
        Patterns = ["*.mp4", "*.webm"],
        MimeTypes = ["video/mp4", "video/webm"]
    };

    private bool _wired;
    private bool _syncingToggle;

    public StudioView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Wire();
        AttachedToVisualTree += async (_, _) =>
        {
            Wire();
            if (DataContext is StudioViewModel vm)
            {
                await vm.LoadAsync();
            }
        };
    }

    private void Wire()
    {
        if (DataContext is not StudioViewModel vm || _wired)
        {
            return;
        }

        _wired = true;
        vm.SetAddGameHandler(() => GetMain()?.NavigateToPublish());
        vm.SetAddDlcHandler(symbol => GetMain()?.NavigateToPublish(symbol));
        vm.SetConfirmDeleteHandler(ConfirmDeleteAsync);
        vm.SetPickImageHandler(() => PickAsync(ImageFiles));
        vm.SetPickVideoHandler(() => PickAsync(VideoFiles));
    }

    private static MainWindowViewModel? GetMain() =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.DataContext is MainWindowViewModel main
            ? main
            : null;

    private async void Refresh_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private void AddGame_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as StudioViewModel)?.AddGameCommand.Execute(null);

    private void AddVersion_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as StudioViewModel)?.AddVersionCommand.Execute(null);

    private void AddDlc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            vm.AddDlcCommand.Execute(vm.SelectedGame);
        }
    }

    private async void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.DeleteCommand.ExecuteAsync(vm.SelectedGame);
        }
    }

    private async void DeleteDlc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm && sender is Control { Tag: StudioContentItem item })
        {
            await vm.DeleteCommand.ExecuteAsync(item);
        }
    }

    private async void SaveStore_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.SaveStoreCommand.ExecuteAsync(null);
        }
    }

    private async void PickCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.PickCoverCommand.ExecuteAsync(null);
        }
    }

    private async void ClearCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.ClearCoverCommand.ExecuteAsync(null);
        }
    }

    private async void PickHeader_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.PickHeaderCommand.ExecuteAsync(null);
        }
    }

    private async void ClearHeader_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.ClearHeaderCommand.ExecuteAsync(null);
        }
    }

    private async void PickIntro_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.PickIntroCommand.ExecuteAsync(null);
        }
    }

    private async void ClearIntro_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.ClearIntroCommand.ExecuteAsync(null);
        }
    }

    private async void AddScreenshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.AddScreenshotCommand.ExecuteAsync(null);
        }
    }

    private async void RemoveScreenshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm && sender is Control { Tag: StudioScreenshotItem shot })
        {
            await vm.RemoveScreenshotCommand.ExecuteAsync(shot);
        }
    }

    private async void SaveNotes_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm && sender is Control { Tag: StudioVersionItem version })
        {
            await vm.SaveVersionNotesCommand.ExecuteAsync(version);
        }
    }

    private async void Published_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingToggle || DataContext is not StudioViewModel vm || vm.SelectedGame is null)
        {
            return;
        }

        _syncingToggle = true;
        try
        {
            await vm.SetDraftCommand.ExecuteAsync(PublishedSwitch.IsChecked == true);
        }
        finally
        {
            _syncingToggle = false;
        }
    }

    private async Task<string?> PickAsync(FilePickerFileType type)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return null;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [type]
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private async Task<bool> ConfirmDeleteAsync(StudioContentItem item)
    {
        var dialog = new FAContentDialog
        {
            Title = item.IsDlc ? "Usunąć DLC?" : "Usunąć grę?",
            Content = $"„{item.Title}” zniknie z katalogu. Tej operacji nie da się cofnąć.",
            PrimaryButtonText = "Usuń",
            CloseButtonText = "Anuluj",
            DefaultButton = FAContentDialogButton.Close
        };
        return await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary;
    }
}
