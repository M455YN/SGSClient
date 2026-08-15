using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using SGSClient.Core.Packaging;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class AdminView : UserControl
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

    public AdminView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is AdminViewModel vm)
            {
                WireHandlers(vm);
            }
        };
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is AdminViewModel vm)
            {
                WireHandlers(vm);
                _ = vm.LoadAsync();
            }
        };
    }

    private void WireHandlers(AdminViewModel vm)
    {
        vm.SetEditHandler(ShowEditDialogAsync);
        vm.SetConfirmDeleteHandler(ConfirmDeleteAsync);
    }

    private async void Refresh_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private async void Edit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: AdminGameItem item })
        {
            await vm.EditCommand.ExecuteAsync(item);
        }
    }

    private async void ToggleDraft_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: AdminGameItem item })
        {
            await vm.ToggleDraftCommand.ExecuteAsync(item);
        }
    }

    private async void ToggleFeatured_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: AdminGameItem item })
        {
            await vm.ToggleFeaturedCommand.ExecuteAsync(item);
        }
    }

    private async void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AdminViewModel vm && sender is Control { Tag: AdminGameItem item })
        {
            await vm.DeleteCommand.ExecuteAsync(item);
        }
    }

    private async Task<bool> ShowEditDialogAsync(AdminGameItem item)
    {
        var genreOptions = SgsGameTaxonomy.Genres.Select(g => g.Symbol).Prepend("").ToArray();
        var engineOptions = SgsGameTaxonomy.Engines.Select(e => e.Symbol).Prepend("").ToArray();

        var titleBox = new TextBox { Text = item.Title, PlaceholderText = "np. Demo Runner" };
        var descBox = new TextBox
        {
            Text = item.Description ?? string.Empty,
            PlaceholderText = "Krótki opis widoczny w sklepie",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 88
        };
        var hardwareBox = new TextBox
        {
            Text = item.HardwareRequirements ?? string.Empty,
            PlaceholderText = "opcjonalnie",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 56
        };
        var otherBox = new TextBox
        {
            Text = item.OtherInformation ?? string.Empty,
            PlaceholderText = "opcjonalnie",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 56
        };
        var genreCombo = new ComboBox
        {
            ItemsSource = genreOptions,
            SelectedItem = genreOptions.Contains(item.GenreSymbol ?? string.Empty)
                ? item.GenreSymbol ?? string.Empty
                : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var engineCombo = new ComboBox
        {
            ItemsSource = engineOptions,
            SelectedItem = engineOptions.Contains(item.EngineSymbol ?? string.Empty)
                ? item.EngineSymbol ?? string.Empty
                : string.Empty,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var draftBox = new CheckBox { Content = "Szkic (ukryty w sklepie)", IsChecked = item.DraftP };
        var featuredBox = new CheckBox { Content = "Wyróżnione", IsChecked = item.FeaturedP };

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
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
                var name = new TextBlock
                {
                    Text = Path.GetFileName(shot.Url.Replace('\\', '/')),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                var remove = new Button { Content = "Usuń", Tag = shot.Id, Padding = new Avalonia.Thickness(10, 4) };
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

        var coverPick = new Button { Content = "Wybierz…", Padding = new Avalonia.Thickness(12, 6) };
        var coverClear = new Button { Content = "Usuń", Padding = new Avalonia.Thickness(12, 6) };
        coverPick.Click += async (_, _) =>
        {
            var path = await PickSingleFileAsync("Wybierz okładkę", ImageFiles);
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

        var introPick = new Button { Content = "Wybierz…", Padding = new Avalonia.Thickness(12, 6) };
        var introClear = new Button { Content = "Usuń", Padding = new Avalonia.Thickness(12, 6) };
        introPick.Click += async (_, _) =>
        {
            var path = await PickSingleFileAsync("Wybierz intro / trailer", VideoFiles);
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
            Padding = new Avalonia.Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
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

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Symbol: {item.Symbol} (bez zmian)",
            Opacity = 0.55,
            FontSize = 12
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
        panel.Children.Add(featuredBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Media",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Margin = new Avalonia.Thickness(0, 16, 0, 4)
        });
        panel.Children.Add(new TextBlock { Text = "Okładka", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(coverLabel);
        panel.Children.Add(BuildButtonRow(coverPick, coverClear));
        panel.Children.Add(new TextBlock
        {
            Text = "Intro / trailer",
            Opacity = 0.55,
            FontSize = 12,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        });
        panel.Children.Add(introLabel);
        panel.Children.Add(BuildButtonRow(introPick, introClear));
        panel.Children.Add(new TextBlock
        {
            Text = "Screeny",
            Opacity = 0.55,
            FontSize = 12,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        });
        panel.Children.Add(screensPanel);
        panel.Children.Add(addShots);

        var dialog = new FAContentDialog
        {
            Title = $"Admin — {item.Symbol}",
            PrimaryButtonText = "Zapisz",
            CloseButtonText = "Anuluj",
            DefaultButton = FAContentDialogButton.Primary,
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 560
            }
        };

        var result = await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        if (result != FAContentDialogResult.Primary)
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
        item.EditFeatured = featuredBox.IsChecked == true;
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

    private async Task<IReadOnlyList<string>> PickMultipleImageFilesAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return [];
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Wybierz screeny",
            AllowMultiple = true,
            FileTypeFilter = [ImageFiles]
        });

        return files.Select(f => f.Path.LocalPath).ToArray();
    }

    private async Task<bool> ConfirmDeleteAsync(AdminGameItem item)
    {
        var dialog = new FAContentDialog
        {
            Title = "Usuń (admin)",
            Content = $"Na pewno usunąć „{item.Title}”? Operacji nie można cofnąć.",
            PrimaryButtonText = "Usuń",
            CloseButtonText = "Anuluj",
            DefaultButton = FAContentDialogButton.Close
        };
        return await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary;
    }
}
