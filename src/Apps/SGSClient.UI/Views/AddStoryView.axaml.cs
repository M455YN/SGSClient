using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class AddStoryView : UserControl
{
    private static readonly FilePickerFileType ImageFiles = new(Loc.T("Obrazy"))
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"],
        AppleUniformTypeIdentifiers = ["public.image"],
        MimeTypes = ["image/*"]
    };

    private static readonly FilePickerFileType TextFiles = new(Loc.T("Tekst"))
    {
        Patterns = ["*.txt", "*.md"],
        AppleUniformTypeIdentifiers = ["public.plain-text"],
        MimeTypes = ["text/plain", "text/markdown"]
    };

    public AddStoryView()
    {
        InitializeComponent();
    }

    private async void PickCover_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddStoryViewModel vm)
        {
            return;
        }

        var path = await AvaloniaStoragePicker.PickSingleFileAsync(
            TopLevel.GetTopLevel(this), ImageFiles, Loc.T("Wybierz okładkę"));
        if (path is not null)
        {
            vm.CoverPath = path;
        }
    }

    private async void ImportFile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddStoryViewModel vm)
        {
            return;
        }

        var path = await AvaloniaStoragePicker.PickSingleFileAsync(
            TopLevel.GetTopLevel(this), TextFiles, Loc.T("Importuj"));
        if (path is not null)
        {
            await vm.ImportFileAsync(path);
        }
    }
}
