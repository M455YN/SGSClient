using SGSClient.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class StudioPage : Page
{
    private bool _syncingToggle;

    public StudioViewModel ViewModel { get; }

    public StudioPage()
    {
        ViewModel = App.Services.GetRequiredService<StudioViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.SetAddGameHandler(NavigateAddGame);
        ViewModel.SetAddDlcHandler(NavigateAddDlc);
        ViewModel.SetConfirmDeleteHandler(ConfirmDeleteAsync);
        ViewModel.SetPickImageHandler(() => PickFileAsync([".png", ".jpg", ".jpeg", ".webp", ".gif"]));
        ViewModel.SetPickVideoHandler(() => PickFileAsync([".mp4", ".webm"]));
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(StudioViewModel.SelectedGame) or nameof(StudioViewModel.SelectedTab))
            {
                SyncPublishedToggle();
            }
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
        SyncPublishedToggle();
    }

    private void SyncPublishedToggle()
    {
        _syncingToggle = true;
        PublishedSwitch.IsOn = ViewModel.SelectedGame?.IsPublished == true;
        _syncingToggle = false;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.LoadCommand.ExecuteAsync(null);

    private void AddGame_Click(object sender, RoutedEventArgs e) => ViewModel.AddGameCommand.Execute(null);

    private void AddVersion_Click(object sender, RoutedEventArgs e) => ViewModel.AddVersionCommand.Execute(null);

    private void AddDlc_Click(object sender, RoutedEventArgs e) => ViewModel.AddDlcCommand.Execute(ViewModel.SelectedGame);

    private async void Delete_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.DeleteCommand.ExecuteAsync(ViewModel.SelectedGame);

    private async void DeleteDlc_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioContentItem item })
        {
            await ViewModel.DeleteCommand.ExecuteAsync(item);
        }
    }

    private async void SaveStore_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.SaveStoreCommand.ExecuteAsync(null);

    private async void PickCover_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.PickCoverCommand.ExecuteAsync(null);

    private async void ClearCover_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ClearCoverCommand.ExecuteAsync(null);

    private async void PickHeader_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.PickHeaderCommand.ExecuteAsync(null);

    private async void ClearHeader_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ClearHeaderCommand.ExecuteAsync(null);

    private async void PickLogo_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.PickLogoCommand.ExecuteAsync(null);

    private async void ClearLogo_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ClearLogoCommand.ExecuteAsync(null);

    private async void PickIntro_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.PickIntroCommand.ExecuteAsync(null);

    private async void ClearIntro_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ClearIntroCommand.ExecuteAsync(null);

    private async void AddScreenshot_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.AddScreenshotCommand.ExecuteAsync(null);

    private async void RemoveScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioScreenshotItem shot })
        {
            await ViewModel.RemoveScreenshotCommand.ExecuteAsync(shot);
        }
    }

    private async void SaveNotes_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioVersionItem version })
        {
            await ViewModel.SaveVersionNotesCommand.ExecuteAsync(version);
        }
    }

    private async void Published_Toggled(object sender, RoutedEventArgs e)
    {
        if (_syncingToggle || ViewModel.SelectedGame is null)
        {
            return;
        }

        await ViewModel.SetDraftCommand.ExecuteAsync(PublishedSwitch.IsOn);
        SyncPublishedToggle();
    }

    private void Tab_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string tag)
        {
            ViewModel.SelectedTab = tag;
        }
    }

    private void NavigateAddGame() =>
        App.MainWindow?.NavigateTo(typeof(PublishPage), parameter: null, selectTag: "Studio");

    private void NavigateAddDlc(string parentSymbol) =>
        App.MainWindow?.NavigateTo(typeof(PublishPage), parentSymbol, "Studio");

    private async Task<string?> PickFileAsync(IReadOnlyList<string> extensions)
    {
        if (App.MainWindow is null)
        {
            return null;
        }

        return await WinUiFilePicker.PickSingleFileAsync(
            App.MainWindow,
            extensions,
            commitLabel: Loc.T("Wybierz"));
    }

    private async Task<bool> ConfirmDeleteAsync(StudioContentItem item)
    {
        var dialog = new ContentDialog
        {
            Title = item.IsDlc ? Loc.T("Usunąć DLC?") : Loc.T("Usunąć grę?"),
            Content = Loc.F("„{0}” zniknie z katalogu. Tej operacji nie da się cofnąć.", item.Title),
            PrimaryButtonText = Loc.T("Usuń"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
