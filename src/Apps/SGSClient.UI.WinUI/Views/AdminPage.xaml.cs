using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class AdminPage : Page
{
    private bool _syncingToggles;

    public AdminViewModel ViewModel { get; }

    public AdminPage()
    {
        ViewModel = App.Services.GetRequiredService<AdminViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.SetAddVersionHandler(() =>
            App.MainWindow?.NavigateTo(typeof(PublishPage), parameter: null, selectTag: "Admin"));
        ViewModel.SetAddDlcHandler(symbol =>
            App.MainWindow?.NavigateTo(typeof(PublishPage), symbol, "Admin"));
        ViewModel.SetConfirmDeleteHandler(ConfirmDeleteAsync);
        ViewModel.SetPickImageHandler(() => PickFileAsync([".png", ".jpg", ".jpeg", ".webp", ".gif"]));
        ViewModel.SetPickVideoHandler(() => PickFileAsync([".mp4", ".webm"]));
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
        RefreshUi();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AdminViewModel.SelectedGame))
        {
            SyncToggles();
        }

        RefreshUi();
    }

    private void SyncToggles()
    {
        _syncingToggles = true;
        if (PublishedToggle is not null)
        {
            PublishedToggle.IsOn = ViewModel.SelectedGame?.IsPublished == true;
        }

        if (FeaturedToggle is not null)
        {
            FeaturedToggle.IsOn = ViewModel.SelectedGame?.FeaturedP == true;
        }

        _syncingToggles = false;
    }

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;
        EmptyText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        GamesTab.Visibility = ViewModel.IsGamesTab && !ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        FeaturedTab.Visibility = ViewModel.IsFeaturedTab && !ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        UsersTab.Visibility = ViewModel.IsUsersTab && !ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        SyncToggles();
        RefreshUi();
    }

    private void Tab_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string tag)
        {
            ViewModel.SelectTabCommand.Execute(tag);
            RefreshUi();
        }
    }

    private void EditorTab_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string tag)
        {
            ViewModel.EditorTab = tag;
        }
    }

    private void Filter_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem?.Tag is string tag)
        {
            ViewModel.SetFilterCommand.Execute(tag);
        }
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            ViewModel.SearchText = sender.Text;
        }
    }

    private async void Published_Toggled(object sender, RoutedEventArgs e)
    {
        if (_syncingToggles)
        {
            return;
        }

        await ViewModel.SetDraftCommand.ExecuteAsync(!PublishedToggle.IsOn);
        SyncToggles();
    }

    private async void Featured_Toggled(object sender, RoutedEventArgs e)
    {
        if (_syncingToggles)
        {
            return;
        }

        await ViewModel.SetFeaturedCommand.ExecuteAsync(FeaturedToggle.IsOn);
        SyncToggles();
    }

    private async void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AdminGameItem item })
        {
            await ViewModel.MoveFeaturedUpCommand.ExecuteAsync(item);
        }
    }

    private async void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AdminGameItem item })
        {
            await ViewModel.MoveFeaturedDownCommand.ExecuteAsync(item);
        }
    }

    private void AddVersion_Click(object sender, RoutedEventArgs e) =>
        ViewModel.AddVersionCommand.Execute(null);

    private void AddDlc_Click(object sender, RoutedEventArgs e) =>
        ViewModel.AddDlcCommand.Execute(ViewModel.SelectedGame);

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

    private async void Delete_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.DeleteCommand.ExecuteAsync(ViewModel.SelectedGame);

    private async void DeleteDlc_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AdminGameItem item })
        {
            await ViewModel.DeleteCommand.ExecuteAsync(item);
        }
    }

    private async Task<string?> PickFileAsync(IReadOnlyList<string> extensions)
    {
        if (App.MainWindow is null)
        {
            return null;
        }

        return await WinUiFilePicker.PickSingleFileAsync(
            App.MainWindow,
            extensions,
            commitLabel: "Wybierz");
    }

    private async Task<bool> ConfirmDeleteAsync(AdminGameItem item)
    {
        var dialog = new ContentDialog
        {
            Title = item.IsDlc ? "Usuń DLC (admin)" : "Usuń (admin)",
            Content = $"Na pewno usunąć „{item.Title}”? Operacji nie można cofnąć.",
            PrimaryButtonText = "Usuń",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
