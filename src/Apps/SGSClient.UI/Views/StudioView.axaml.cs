using SGSClient.Core.Localization;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class StudioView : UserControl
{
    private static readonly FilePickerFileType ImageFiles = new(Loc.T("Obrazy"))
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"],
        MimeTypes = ["image/*"]
    };

    private static readonly FilePickerFileType VideoFiles = new(Loc.T("Wideo"))
    {
        Patterns = ["*.mp4", "*.webm"],
        MimeTypes = ["video/mp4", "video/webm"]
    };

    private StudioViewModel? _wiredVm;
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
        if (DataContext is not StudioViewModel vm)
        {
            Debug.WriteLine("[SGS Media] StudioView Wire skipped — DataContext is not StudioViewModel");
            return;
        }

        if (ReferenceEquals(_wiredVm, vm))
        {
            return;
        }

        _wiredVm = vm;
        vm.SetAddGameHandler(() => GetMain()?.NavigateToPublish());
        vm.SetAddDlcHandler(symbol => GetMain()?.NavigateToPublish(symbol));
        vm.SetConfirmDeleteHandler(ConfirmDeleteAsync);
        vm.SetPickImageHandler(() => AvaloniaStoragePicker.PickSingleFileAsync(
            TopLevel.GetTopLevel(this), ImageFiles, Loc.T("Wybierz obraz")));
        vm.SetPickVideoHandler(() => AvaloniaStoragePicker.PickSingleFileAsync(
            TopLevel.GetTopLevel(this), VideoFiles, Loc.T("Wybierz wideo")));
        Debug.WriteLine("[SGS Media] StudioView pick handlers wired");
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
            if (vm.CurrentWorks is { } works)
            {
                await works.LoadAsync();
            }
        }
    }

    private void AddGame_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as StudioViewModel)?.AddGameCommand.Execute(null);

    private void AddNewDlc_OnClick(object? sender, RoutedEventArgs e) =>
        GetMain()?.NavigateToPublish(kind: "dlc");

    private void AddApp_OnClick(object? sender, RoutedEventArgs e) =>
        GetMain()?.NavigateToPublish(kind: "app");

    private void AddArt_OnClick(object? sender, RoutedEventArgs e) =>
        GetMain()?.NavigateToAddMedia("art");

    private void AddMusic_OnClick(object? sender, RoutedEventArgs e) =>
        GetMain()?.NavigateToAddMedia("music");

    private void AddStory_OnClick(object? sender, RoutedEventArgs e) =>
        GetMain()?.NavigateToAddStory();

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

    private async void PickLogo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.PickLogoCommand.ExecuteAsync(null);
        }
    }

    private async void ClearLogo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.ClearLogoCommand.ExecuteAsync(null);
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

    private async Task<bool> ConfirmDeleteAsync(StudioContentItem item)
    {
        var dialog = new FAContentDialog
        {
            Title = item.IsDlc ? Loc.T("Usunąć DLC?") : Loc.T("Usunąć grę?"),
            Content = Loc.F("„{0}” zniknie z katalogu. Tej operacji nie da się cofnąć.", item.Title),
            PrimaryButtonText = Loc.T("Usuń"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = FAContentDialogButton.Close
        };
        return await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary;
    }
}
