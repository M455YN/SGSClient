using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.Core.Packaging;
using SGSClient.UI.WinUI.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class StudioPage : Page
{
    public StudioViewModel ViewModel { get; }

    public StudioPage()
    {
        ViewModel = App.Services.GetRequiredService<StudioViewModel>();
        InitializeComponent();
        ViewModel.SetAddGameHandler(NavigateAddGame);
        ViewModel.SetAddDlcHandler(NavigateAddDlc);
        ViewModel.SetEditHandler(ShowEditDialogAsync);
        ViewModel.SetConfirmDeleteHandler(ConfirmDeleteAsync);
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        TitleText.Text = ViewModel.Title;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
        RefreshUi();
    }

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;
        EmptyText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        GamesList.Visibility = ViewModel.HasGames ? Visibility.Visible : Visibility.Collapsed;
        GamesList.ItemsSource = null;
        GamesList.ItemsSource = ViewModel.Games;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void AddGame_Click(object sender, RoutedEventArgs e) => ViewModel.AddGameCommand.Execute(null);

    private void AddDlc_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioContentItem item })
        {
            ViewModel.AddDlcCommand.Execute(item);
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioContentItem item })
        {
            await ViewModel.EditCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioContentItem item })
        {
            await ViewModel.DeleteCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private void NavigateAddGame()
    {
        App.MainWindow?.NavigateTo(typeof(PublishPage), parameter: null, selectTag: "Publish");
    }

    private void NavigateAddDlc(string parentSymbol)
    {
        App.MainWindow?.NavigateTo(typeof(PublishPage), parentSymbol, "Publish");
    }

    private async Task<bool> ShowEditDialogAsync(StudioContentItem item)
    {
        var genreOptions = SgsGameTaxonomy.Genres.Select(g => g.Symbol).Prepend("").ToArray();
        var engineOptions = SgsGameTaxonomy.Engines.Select(e => e.Symbol).Prepend("").ToArray();

        var titleBox = new TextBox
        {
            Text = item.Title,
            PlaceholderText = "np. Demo Runner",
            Margin = new Thickness(0, 0, 0, 8)
        };
        var descBox = new TextBox
        {
            Text = item.Description ?? string.Empty,
            PlaceholderText = "Krótki opis widoczny w sklepie",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 88,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var hardwareBox = new TextBox
        {
            Text = item.HardwareRequirements ?? string.Empty,
            PlaceholderText = "opcjonalnie",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 56,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var otherBox = new TextBox
        {
            Text = item.OtherInformation ?? string.Empty,
            PlaceholderText = "opcjonalnie",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 56,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var genreCombo = new ComboBox
        {
            ItemsSource = genreOptions,
            SelectedItem = genreOptions.Contains(item.GenreSymbol ?? string.Empty)
                ? item.GenreSymbol ?? string.Empty
                : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var engineCombo = new ComboBox
        {
            ItemsSource = engineOptions,
            SelectedItem = engineOptions.Contains(item.EngineSymbol ?? string.Empty)
                ? item.EngineSymbol ?? string.Empty
                : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var draftBox = new CheckBox
        {
            Content = "Szkic (ukryty w sklepie)",
            IsChecked = item.DraftP
        };

        item.PendingCoverPath = null;
        item.PendingIntroPath = null;
        item.ClearCover = false;
        item.ClearIntro = false;
        item.PendingScreenshotPaths.Clear();
        item.ScreenshotIdsToRemove.Clear();

        var coverLabel = new TextBlock
        {
            Text = MediaLabel("Okładka", item.CoverUrl, null),
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap
        };
        var introLabel = new TextBlock
        {
            Text = MediaLabel("Intro", item.IntroUrl, null),
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap
        };
        var screensPanel = new StackPanel { Spacing = 6 };
        void RebuildScreens()
        {
            screensPanel.Children.Clear();
            foreach (var shot in item.Screenshots.Where(s => !item.ScreenshotIdsToRemove.Contains(s.Id)))
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock
                {
                    Text = Path.GetFileName(shot.Url.Replace('\\', '/')),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                var remove = new Button { Content = "Usuń", Tag = shot.Id, Padding = new Thickness(10, 4, 10, 4) };
                remove.Click += (_, _) =>
                {
                    if (remove.Tag is int id)
                    {
                        item.ScreenshotIdsToRemove.Add(id);
                        RebuildScreens();
                    }
                };
                Grid.SetColumn(remove, 1);
                row.Children.Add(name);
                row.Children.Add(remove);
                screensPanel.Children.Add(row);
            }

            foreach (var pending in item.PendingScreenshotPaths)
            {
                screensPanel.Children.Add(new TextBlock
                {
                    Text = $"+ {Path.GetFileName(pending)}",
                    Opacity = 0.7
                });
            }
        }

        RebuildScreens();

        var coverPick = new Button { Content = "Wybierz…", Padding = new Thickness(12, 6, 12, 6) };
        var coverClear = new Button { Content = "Usuń", Padding = new Thickness(12, 6, 12, 6) };
        coverPick.Click += async (_, _) =>
        {
            var path = await PickSingleFileAsync([".png", ".jpg", ".jpeg", ".webp", ".gif"]);
            if (path is null)
            {
                return;
            }

            item.PendingCoverPath = path;
            item.ClearCover = false;
            coverLabel.Text = MediaLabel("Okładka", item.CoverUrl, path);
        };
        coverClear.Click += (_, _) =>
        {
            item.PendingCoverPath = null;
            item.ClearCover = true;
            coverLabel.Text = "Okładka: (do usunięcia)";
        };

        var introPick = new Button { Content = "Wybierz…", Padding = new Thickness(12, 6, 12, 6) };
        var introClear = new Button { Content = "Usuń", Padding = new Thickness(12, 6, 12, 6) };
        introPick.Click += async (_, _) =>
        {
            var path = await PickSingleFileAsync([".mp4", ".webm"]);
            if (path is null)
            {
                return;
            }

            item.PendingIntroPath = path;
            item.ClearIntro = false;
            introLabel.Text = MediaLabel("Intro", item.IntroUrl, path);
        };
        introClear.Click += (_, _) =>
        {
            item.PendingIntroPath = null;
            item.ClearIntro = true;
            introLabel.Text = "Intro: (do usunięcia)";
        };

        var addShots = new Button
        {
            Content = "Dodaj screeny…",
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0)
        };
        addShots.Click += async (_, _) =>
        {
            var paths = await PickMultipleImageFilesAsync();
            if (paths.Count == 0)
            {
                return;
            }

            var remaining = SgsPackageRules.MaxScreenshots
                - item.Screenshots.Count(s => !item.ScreenshotIdsToRemove.Contains(s.Id))
                - item.PendingScreenshotPaths.Count;
            foreach (var path in paths.Take(Math.Max(0, remaining)))
            {
                if (!item.PendingScreenshotPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    item.PendingScreenshotPaths.Add(path);
                }
            }

            RebuildScreens();
        };

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Symbol: {item.Symbol} (bez zmian)",
            Opacity = 0.55,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 8)
        });
        panel.Children.Add(new TextBlock { Text = "Nazwa", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(titleBox);
        panel.Children.Add(new TextBlock { Text = "Opis", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(descBox);
        panel.Children.Add(new TextBlock { Text = "Wymagania sprzętowe", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(hardwareBox);
        panel.Children.Add(new TextBlock { Text = "Inne informacje", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(otherBox);
        panel.Children.Add(new TextBlock { Text = "Gatunek", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(genreCombo);
        panel.Children.Add(new TextBlock { Text = "Silnik", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(engineCombo);
        panel.Children.Add(draftBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Media",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 4)
        });
        panel.Children.Add(new TextBlock { Text = "Okładka", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(coverLabel);
        panel.Children.Add(BuildButtonRow(coverPick, coverClear));
        panel.Children.Add(new TextBlock { Text = "Intro / trailer", Opacity = 0.55, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(introLabel);
        panel.Children.Add(BuildButtonRow(introPick, introClear));
        panel.Children.Add(new TextBlock { Text = "Screeny", Opacity = 0.55, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
        panel.Children.Add(screensPanel);
        panel.Children.Add(addShots);

        var dialog = new ContentDialog
        {
            Title = item.IsDlc ? $"Edytuj DLC — {item.Symbol}" : $"Edytuj grę — {item.Symbol}",
            PrimaryButtonText = "Zapisz",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 560,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        item.EditTitle = titleBox.Text ?? string.Empty;
        item.EditDescription = descBox.Text;
        item.EditHardwareRequirements = hardwareBox.Text;
        item.EditOtherInformation = otherBox.Text;
        item.EditGenre = genreCombo.SelectedItem as string ?? string.Empty;
        item.EditEngine = engineCombo.SelectedItem as string ?? string.Empty;
        item.EditDraft = draftBox.IsChecked == true;
        return true;
    }

    private static StackPanel BuildButtonRow(params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }

        return row;
    }

    private static string MediaLabel(string kind, string? currentUrl, string? pendingPath)
    {
        if (!string.IsNullOrWhiteSpace(pendingPath))
        {
            return $"{kind}: {Path.GetFileName(pendingPath)} (nowy plik)";
        }

        if (string.IsNullOrWhiteSpace(currentUrl))
        {
            return $"{kind}: (brak)";
        }

        return $"{kind}: {Path.GetFileName(currentUrl.Replace('\\', '/'))}";
    }

    private async Task<string?> PickSingleFileAsync(IEnumerable<string> extensions)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        foreach (var ext in extensions)
        {
            picker.FileTypeFilter.Add(ext);
        }

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<IReadOnlyList<string>> PickMultipleImageFilesAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif" })
        {
            picker.FileTypeFilter.Add(ext);
        }

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        var files = await picker.PickMultipleFilesAsync();
        return files?.Select(f => f.Path).ToArray() ?? [];
    }

    private async Task<bool> ConfirmDeleteAsync(StudioContentItem item)
    {
        var dialog = new ContentDialog
        {
            Title = "Usuń treść",
            Content = $"Czy na pewno usunąć „{item.Title}”? Tej operacji nie można cofnąć.",
            PrimaryButtonText = "Usuń",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
