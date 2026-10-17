using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class ContentPublishView : UserControl
{
    private static readonly Dictionary<string, string[]> FilePatterns = new()
    {
        ["book"] = ["*.pdf", "*.epub", "*.mobi", "*.txt", "*.md", "*.docx", "*.zip"],
        ["music"] = ["*.mp3", "*.flac", "*.wav", "*.ogg", "*.m4a", "*.zip"],
        ["art"] = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.svg", "*.psd", "*.zip"]
    };

    public ContentPublishView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is ContentPublishViewModel vm)
            {
                vm.RefreshAuthHint();
                SyncKindRadios(vm);
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ContentPublishViewModel.Kind))
                    {
                        SyncKindRadios(vm);
                    }
                };
            }
        };
    }

    private void SyncKindRadios(ContentPublishViewModel vm)
    {
        foreach (var radio in KindPanel.Children.OfType<RadioButton>())
        {
            radio.IsChecked = (string?)radio.Tag == vm.Kind;
        }
    }

    private void Kind_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ContentPublishViewModel vm && sender is RadioButton { Tag: string kind })
        {
            vm.Kind = kind;
        }
    }

    private async void PickFiles_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContentPublishViewModel vm || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("Wybierz pliki"),
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType(Loc.T("Pliki")) { Patterns = FilePatterns[vm.Kind] }]
        });
        vm.AddFiles(files.Select(f => f.Path.LocalPath));
    }

    private void ClearFiles_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as ContentPublishViewModel)?.ClearFiles();

    private async void PickCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ContentPublishViewModel vm || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc.T("Wybierz okładkę"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc.T("Obrazy")) { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"] }
            ]
        });
        if (files.Count > 0)
        {
            vm.CoverPath = files[0].Path.LocalPath;
        }
    }
}
