using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class AddMediaView : UserControl
{
    public AddMediaView()
    {
        InitializeComponent();
    }

    private AddMediaViewModel? ViewModel => DataContext as AddMediaViewModel;

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = ViewModel is { IsBusy: false } && e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (ViewModel is { IsBusy: false } vm && files is not null)
        {
            vm.AddFiles(files.OfType<IStorageFile>().Select(file => file.Path.LocalPath));
        }
    }

    private async void OnAddFiles(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        var paths = await AvaloniaStoragePicker.PickMultipleFilesAsync(
            TopLevel.GetTopLevel(this),
            FileTypeFor(vm),
            vm.AddFilesLabel);
        vm.AddFiles(paths);
    }

    private async void OnPickCover(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        var type = new FilePickerFileType(Loc.T("Obrazy"))
        {
            Patterns = AddMediaViewModel.ArtExtensions.Select(ext => "*" + ext).ToArray()
        };
        var path = await AvaloniaStoragePicker.PickSingleFileAsync(TopLevel.GetTopLevel(this), type, Loc.T("Wybierz okładkę"));
        if (path is not null)
        {
            vm.CoverPath = path;
        }
    }

    private static FilePickerFileType FileTypeFor(AddMediaViewModel vm) => new(vm.IsMusic ? Loc.T("Utwory") : Loc.T("Obrazy"))
    {
        Patterns = vm.Extensions.Select(ext => "*" + ext).ToArray()
    };
}
