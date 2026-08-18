using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class PublishView : UserControl
{
    private static readonly FilePickerFileType ImageFiles = new("Obrazy")
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"],
        AppleUniformTypeIdentifiers = ["public.image"],
        MimeTypes = ["image/*"]
    };

    private static readonly FilePickerFileType VideoFiles = new("Wideo")
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
                SyncKindRadios(vm);
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName is nameof(PublishViewModel.IsDlc)
                        or nameof(PublishViewModel.ShowParentField))
                    {
                        SyncKindRadios(vm);
                    }
                };
            }
        };
    }

    private void SyncKindRadios(PublishViewModel vm)
    {
        KindGameRadio.IsChecked = !vm.IsDlc;
        KindDlcRadio.IsChecked = vm.IsDlc;
    }

    private void KindGame_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            vm.IsDlc = false;
        }
    }

    private void KindDlc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PublishViewModel vm)
        {
            vm.IsDlc = true;
        }
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
            Title = "Wybierz folder gry",
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

        var path = await PickSingleFileAsync("Wybierz miniaturkę", ImageFiles);
        if (path is null)
        {
            return;
        }

        if (!vm.TrySetCoverFromFile(path, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? "Nie udało się ustawić miniaturki.");
            vm.StatusText = "Nie udało się dodać miniaturki.";
        }
    }

    private async void PickHeader_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PublishViewModel vm)
        {
            return;
        }

        var path = await PickSingleFileAsync("Wybierz okładkę poziomą", ImageFiles);
        if (path is null)
        {
            return;
        }

        if (!vm.TrySetHeaderFromFile(path, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? "Nie udało się ustawić okładki poziomej.");
            vm.StatusText = "Nie udało się dodać okładki poziomej.";
        }
    }

    private async void PickIntro_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PublishViewModel vm)
        {
            return;
        }

        var path = await PickSingleFileAsync("Wybierz intro / trailer", VideoFiles);
        if (path is null)
        {
            return;
        }

        if (!vm.TrySetIntroFromFile(path, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? "Nie udało się ustawić intro.");
            vm.StatusText = "Nie udało się dodać intro.";
        }
    }

    private async void PickScreenshots_OnClick(object? sender, RoutedEventArgs e)
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

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Wybierz screeny",
            AllowMultiple = true,
            FileTypeFilter = [ImageFiles]
        });

        if (files.Count == 0)
        {
            return;
        }

        var paths = files.Select(f => f.Path.LocalPath).ToArray();
        if (!vm.TryAddScreenshotsFromFiles(paths, out var error))
        {
            vm.Issues.Clear();
            vm.Issues.Add(error ?? "Nie udało się dodać screenów.");
            vm.StatusText = "Nie udało się dodać screenów.";
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

    private async Task<string?> PickSingleFileAsync(string title, FilePickerFileType type)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return null;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [type]
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
