using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class AddMediaPage : Page
{
    public AddMediaViewModel ViewModel { get; }

    public AddMediaPage()
    {
        ViewModel = App.Services.GetRequiredService<AddMediaViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        ViewModel.Entries.CollectionChanged += (_, _) => RefreshUi();
        RefreshUi();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.PrepareFor(e.Parameter as string is { Length: > 0 } kind ? kind : "art");
        RefreshUi();
    }

    private void RefreshUi()
    {
        TitleText.Text = ViewModel.PageTitle;
        DropHintText.Text = ViewModel.DropHint;
        AuthHintText.Text = ViewModel.AuthHint;
        AddFilesButton.Content = ViewModel.AddFilesLabel;
        StatusText.Text = ViewModel.StatusText;
        EntriesList.Visibility = ToVisibility(ViewModel.HasEntries);
        DetailsBorder.Visibility = ToVisibility(ViewModel.HasEntries);
        ActionsPanel.Visibility = ToVisibility(ViewModel.HasEntries);
        CoverPanel.Visibility = ToVisibility(ViewModel.ShowCover);
        CoverBox.Text = ViewModel.CoverPath;
        ProgressPanel.Visibility = ToVisibility(ViewModel.IsBusy);
        UploadProgress.Value = ViewModel.Progress;
        AddFilesButton.IsEnabled = !ViewModel.IsBusy;
        PickCoverButton.IsEnabled = !ViewModel.IsBusy;
        PublishButton.IsEnabled = !ViewModel.IsBusy;
        ClearButton.IsEnabled = !ViewModel.IsBusy;
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var paths = await WinUiFilePicker.PickMultipleFilesAsync(App.MainWindow, ViewModel.Extensions, Loc.T("Dodaj"));
        ViewModel.AddFiles(paths);
    }

    private async void PickCover_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var path = await WinUiFilePicker.PickSingleFileAsync(
            App.MainWindow,
            [".png", ".jpg", ".jpeg", ".webp", ".gif"],
            Loc.T("Wybierz okładkę"));
        if (path is not null)
        {
            ViewModel.CoverPath = path;
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        var accept = !ViewModel.IsBusy && e.DataView.Contains(StandardDataFormats.StorageItems);
        e.AcceptedOperation = accept ? DataPackageOperation.Copy : DataPackageOperation.None;
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel.IsBusy || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            ViewModel.AddFiles(items.OfType<StorageFile>().Select(file => file.Path));
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void Thumb_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border { DataContext: UploadEntry entry } tile)
        {
            return;
        }

        tile.Child = ViewModel.IsMusic
            ? new TextBlock
            {
                Text = "♪",
                FontSize = 28,
                Opacity = 0.7,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
            : new Image
            {
                Source = new BitmapImage(new Uri(entry.Path)) { DecodePixelWidth = 72 },
                Stretch = Stretch.UniformToFill
            };
    }

    private void RemoveEntry_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveCommand.Execute(((Button)sender).Tag);

    private void Clear_Click(object sender, RoutedEventArgs e) => ViewModel.ClearCommand.Execute(null);

    private async void Publish_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.PublishAllCommand.ExecuteAsync(null);
}
