using SGSClient.Core.Localization;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class AdminView : UserControl
{
    private static readonly FilePickerFileType ImageFiles = new(Loc.T("Obrazy"))
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"],
        AppleUniformTypeIdentifiers = ["public.image"],
        MimeTypes = ["image/*"]
    };

    private static readonly FilePickerFileType VideoFiles = new(Loc.T("Wideo"))
    {
        Patterns = ["*.mp4", "*.webm"],
        AppleUniformTypeIdentifiers = ["public.movie"],
        MimeTypes = ["video/mp4", "video/webm"]
    };

    private AdminViewModel? _wiredVm;

    public AdminView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireHandlers();
        AttachedToVisualTree += (_, _) =>
        {
            WireHandlers();
            if (DataContext is AdminViewModel vm)
            {
                _ = vm.LoadAsync();
            }
        };
    }

    private void WireHandlers()
    {
        if (DataContext is not AdminViewModel vm)
        {
            Debug.WriteLine("[SGS Media] AdminView WireHandlers skipped — DataContext is not AdminViewModel");
            return;
        }

        if (ReferenceEquals(_wiredVm, vm))
        {
            return;
        }

        _wiredVm = vm;
        vm.SetAddVersionHandler(id => GetMain()?.NavigateToPublish(selectTag: "Admin", versionOfGameId: id));
        vm.SetSaveFileHandler((name, content) =>
            AvaloniaStoragePicker.SaveTextFileAsync(TopLevel.GetTopLevel(this), name, content));
        vm.SetAddDlcHandler(symbol => GetMain()?.NavigateToPublish(symbol, "Admin"));
        vm.SetConfirmDeleteHandler(ConfirmDeleteAsync);
        vm.SetPickImageHandler(() => AvaloniaStoragePicker.PickSingleFileAsync(
            TopLevel.GetTopLevel(this), ImageFiles, Loc.T("Wybierz obraz")));
        vm.SetPickVideoHandler(() => AvaloniaStoragePicker.PickSingleFileAsync(
            TopLevel.GetTopLevel(this), VideoFiles, Loc.T("Wybierz wideo")));
        Debug.WriteLine("[SGS Media] AdminView pick handlers wired");
    }

    private static MainWindowViewModel? GetMain() =>
        Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.DataContext is MainWindowViewModel main
            ? main
            : null;

    private async void Refresh_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
            if (vm.CurrentWorks is { } works)
            {
                await works.LoadAsync();
            }
        }
    }

    private async void MoveUp_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: AdminGameItem item })
        {
            await vm.MoveFeaturedUpCommand.ExecuteAsync(item);
        }
    }

    private async void MoveDown_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: AdminGameItem item })
        {
            await vm.MoveFeaturedDownCommand.ExecuteAsync(item);
        }
    }

    private void AddVersion_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as AdminViewModel)?.AddVersionCommand.Execute(null);

    private void AddDlc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            vm.AddDlcCommand.Execute(vm.SelectedGame);
        }
    }

    private async void SaveStore_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.SaveStoreCommand.ExecuteAsync(null);
        }
    }

    private async void PickCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.PickCoverCommand.ExecuteAsync(null);
        }
    }

    private async void ClearCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.ClearCoverCommand.ExecuteAsync(null);
        }
    }

    private async void PickHeader_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.PickHeaderCommand.ExecuteAsync(null);
        }
    }

    private async void ClearHeader_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.ClearHeaderCommand.ExecuteAsync(null);
        }
    }

    private async void PickLogo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.PickLogoCommand.ExecuteAsync(null);
        }
    }

    private async void ClearLogo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.ClearLogoCommand.ExecuteAsync(null);
        }
    }

    private async void PickIntro_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.PickIntroCommand.ExecuteAsync(null);
        }
    }

    private async void ClearIntro_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.ClearIntroCommand.ExecuteAsync(null);
        }
    }

    private async void AddScreenshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.AddScreenshotCommand.ExecuteAsync(null);
        }
    }

    private async void RemoveScreenshot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: StudioScreenshotItem shot })
        {
            await vm.RemoveScreenshotCommand.ExecuteAsync(shot);
        }
    }

    private async void SaveNotes_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: StudioVersionItem version })
        {
            await vm.SaveVersionNotesCommand.ExecuteAsync(version);
        }
    }

    private async void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.DeleteCommand.ExecuteAsync(vm.SelectedGame);
        }
    }

    private async void DeleteDlc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: AdminGameItem item })
        {
            await vm.DeleteCommand.ExecuteAsync(item);
        }
    }

    private async Task<bool> ConfirmDeleteAsync(AdminGameItem item)
    {
        var dialog = new FAContentDialog
        {
            Title = item.IsDlc ? Loc.T("Usuń DLC (admin)") : Loc.T("Usuń (admin)"),
            Content = Loc.F("Na pewno usunąć „{0}”? Operacji nie można cofnąć.", item.Title),
            PrimaryButtonText = Loc.T("Usuń"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = FAContentDialogButton.Close
        };
        return await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary;
    }
}
