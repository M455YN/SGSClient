using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class AddStoryPage : Page
{
    private bool _syncing;

    public AddStoryViewModel ViewModel { get; }

    public AddStoryPage()
    {
        ViewModel = App.Services.GetRequiredService<AddStoryViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AddStoryViewModel.SelectedChapter))
            {
                PushChapterToEditor();
            }

            RefreshUi();
        };
        PushChapterToEditor();
        RefreshUi();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Prepare();
        RefreshUi();
    }

    private void RefreshUi()
    {
        TitleText.Text = ViewModel.PageTitle;
        AuthHintText.Text = ViewModel.AuthHint;
        StatusText.Text = ViewModel.StatusText;
        EditorBorder.Visibility = ViewModel.SelectedChapter is null ? Visibility.Collapsed : Visibility.Visible;
        RemoveChapterButton.IsEnabled = ViewModel.SelectedChapter is not null && !ViewModel.IsBusy;
        PublishButton.IsEnabled = !ViewModel.IsBusy;
        ProgressPanel.Visibility = ViewModel.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        PublishProgress.Value = ViewModel.Progress;
    }

    private void PushChapterToEditor()
    {
        _syncing = true;
        try
        {
            ChapterTitleBox.Text = ViewModel.SelectedChapter?.Title ?? string.Empty;
            ChapterTextBox.Text = ViewModel.SelectedChapter?.Text ?? string.Empty;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ChapterTitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_syncing && ViewModel.SelectedChapter is { } chapter)
        {
            chapter.Title = ChapterTitleBox.Text;
        }
    }

    private void ChapterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_syncing && ViewModel.SelectedChapter is { } chapter)
        {
            chapter.Text = ChapterTextBox.Text;
        }
    }

    private void AddChapter_Click(object sender, RoutedEventArgs e) =>
        ViewModel.AddChapterCommand.Execute(null);

    private void RemoveChapter_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveChapterCommand.Execute(ViewModel.SelectedChapter);

    private async void PickCover_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var path = await WinUiFilePicker.PickSingleFileAsync(
            App.MainWindow,
            [".png", ".jpg", ".jpeg", ".webp", ".gif"],
            commitLabel: Loc.T("Wybierz okładkę"));
        if (path is not null)
        {
            ViewModel.CoverPath = path;
        }
    }

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var path = await WinUiFilePicker.PickSingleFileAsync(
            App.MainWindow,
            [".txt", ".md"],
            commitLabel: Loc.T("Importuj"));
        if (path is not null)
        {
            await ViewModel.ImportFileAsync(path);
        }
    }

    private async void Publish_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PublishCommand.ExecuteAsync(null);
    }
}
