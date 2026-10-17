using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class ContentPublishPage : Page
{
    private static readonly Dictionary<string, string[]> FileTypes = new()
    {
        ["book"] = [".pdf", ".epub", ".mobi", ".txt", ".md", ".docx", ".zip"],
        ["music"] = [".mp3", ".flac", ".wav", ".ogg", ".m4a", ".zip"],
        ["art"] = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg", ".psd", ".zip"]
    };

    private bool _syncing;

    public ContentPublishViewModel ViewModel { get; }

    public ContentPublishPage()
    {
        ViewModel = App.Services.GetRequiredService<ContentPublishViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        RefreshUi();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.PrepareFor(e.Parameter is string { Length: > 0 } kind ? kind : ViewModel.Kind);
        RefreshUi();
    }

    private void RefreshUi()
    {
        _syncing = true;
        TitleText.Text = ViewModel.PageTitle;
        AuthHintText.Text = ViewModel.AuthHint;
        StatusText.Text = ViewModel.StatusText;
        FilesText.Text = ViewModel.FilesSummary;
        CoverText.Text = string.IsNullOrWhiteSpace(ViewModel.CoverPath) ? "—" : ViewModel.CoverPath;
        PublishButton.IsEnabled = !ViewModel.IsBusy;
        ProgressPanel.Visibility = ViewModel.ShowProgress ? Visibility.Visible : Visibility.Collapsed;
        UploadProgress.Value = ViewModel.Progress;
        IssuesList.ItemsSource = ViewModel.Issues.ToList();
        foreach (var radio in KindPanel.Children.OfType<RadioButton>())
        {
            radio.IsChecked = (string)radio.Tag == ViewModel.Kind;
        }

        SetText(TitleBox, ViewModel.ContentTitle);
        SetText(SymbolBox, ViewModel.ContentSymbol);
        SetText(DescriptionBox, ViewModel.ContentDescription);
        SetText(VersionBox, ViewModel.Version);
        _syncing = false;
    }

    private static void SetText(TextBox box, string value)
    {
        if (box.Text != value)
        {
            box.Text = value;
        }
    }

    private void Kind_Checked(object sender, RoutedEventArgs e)
    {
        if (!_syncing && sender is RadioButton { Tag: string kind })
        {
            ViewModel.Kind = kind;
        }
    }

    private void Form_Changed(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        // Title first: it may rewrite the symbol, which the next line then overwrites with the box value.
        ViewModel.ContentTitle = TitleBox.Text;
        ViewModel.ContentSymbol = SymbolBox.Text;
        ViewModel.ContentDescription = DescriptionBox.Text;
        ViewModel.Version = VersionBox.Text;
    }

    private async void PickFiles_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var paths = await WinUiFilePicker.PickMultipleFilesAsync(
            App.MainWindow,
            FileTypes[ViewModel.Kind],
            Loc.T("Wybierz pliki"),
            startLocation: Microsoft.Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary);
        ViewModel.AddFiles(paths);
        RefreshUi();
    }

    private void ClearFiles_Click(object sender, RoutedEventArgs e) => ViewModel.ClearFiles();

    private async void PickCover_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var path = await WinUiFilePicker.PickSingleFileAsync(
            App.MainWindow, [".png", ".jpg", ".jpeg", ".webp", ".gif"], Loc.T("Wybierz okładkę"));
        if (path is not null)
        {
            ViewModel.CoverPath = path;
        }
    }

    private async void Publish_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PublishCommand.ExecuteAsync(null);
        RefreshUi();
    }
}
