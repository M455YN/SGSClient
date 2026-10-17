using SGSClient.Core.Localization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class PublishView : UserControl
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

    public PublishView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is PublishViewModel vm)
            {
                vm.RefreshAuthHint();
            }
        };
    }

    private async void PickFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PublishViewModel vm)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.T("Wybierz folder gry"),
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            await vm.SetFolderAsync(folders[0].Path.LocalPath);
        }
    }

    private async void PickCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PublishViewModel vm)
        {
            return;
        }

        var path = await PickSingleFileAsync(Loc.T("Wybierz miniaturkę"), ImageFiles);
        if (path is null)
        {
            return;
        }

        if (!vm.TrySetCoverFromFile(path, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? Loc.T("Nie udało się ustawić miniaturki."));
            vm.StatusText = Loc.T("Nie udało się dodać miniaturki.");
        }
    }

    private async void PickHeader_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PublishViewModel vm)
        {
            return;
        }

        var path = await PickSingleFileAsync(Loc.T("Wybierz okładkę poziomą"), ImageFiles);
        if (path is null)
        {
            return;
        }

        if (!vm.TrySetHeaderFromFile(path, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? Loc.T("Nie udało się ustawić okładki poziomej."));
            vm.StatusText = Loc.T("Nie udało się dodać okładki poziomej.");
        }
    }

    private async void PickLogo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PublishViewModel vm)
        {
            return;
        }

        var path = await PickSingleFileAsync(Loc.T("Wybierz logo (kwadratowe)"), ImageFiles);
        if (path is null)
        {
            return;
        }

        if (!vm.TrySetLogoFromFile(path, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? Loc.T("Nie udało się ustawić logo."));
            vm.StatusText = Loc.T("Nie udało się dodać logo.");
        }
    }

    private async void PickIntro_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PublishViewModel vm)
        {
            return;
        }

        var path = await PickSingleFileAsync(Loc.T("Wybierz intro / trailer"), VideoFiles);
        if (path is null)
        {
            return;
        }

        if (!vm.TrySetIntroFromFile(path, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? Loc.T("Nie udało się ustawić intro."));
            vm.StatusText = Loc.T("Nie udało się dodać intro.");
        }
    }

    private async void PickScreenshots_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PublishViewModel vm)
        {
            return;
        }

        var paths = await AvaloniaStoragePicker.PickMultipleFilesAsync(
            TopLevel.GetTopLevel(this), ImageFiles, Loc.T("Wybierz screeny"));
        if (paths.Count == 0)
        {
            return;
        }

        if (!vm.TryAddScreenshotsFromFiles(paths, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? Loc.T("Nie udało się dodać screenów."));
            vm.StatusText = Loc.T("Nie udało się dodać screenów.");
        }
    }

    private void ClearCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            vm.ClearCover();
        }
    }

    private void ClearHeader_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            vm.ClearHeader();
        }
    }

    private void ClearLogo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            vm.ClearLogo();
        }
    }

    private void ClearIntro_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            vm.ClearIntro();
        }
    }

    private void ClearScreenshots_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            vm.ClearScreenshots();
        }
    }

    private async void Prepare_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            await vm.PrepareCommand.ExecuteAsync(null);
        }
    }

    private async void Publish_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            await vm.PublishCommand.ExecuteAsync(null);
        }
    }

    private Task<string?> PickSingleFileAsync(string title, FilePickerFileType type) =>
        AvaloniaStoragePicker.PickSingleFileAsync(TopLevel.GetTopLevel(this), type, title);
}
