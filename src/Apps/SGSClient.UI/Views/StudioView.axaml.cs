using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using SGSClient.Core.Packaging;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class StudioView : UserControl
{
    public StudioView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is StudioViewModel vm)
            {
                WireHandlers(vm);
                _ = vm.LoadAsync();
            }
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            WireHandlers(vm);
        }
    }

    private void WireHandlers(StudioViewModel vm)
    {
        vm.SetAddGameHandler(() => GetMainWindowViewModel()?.NavigateToPublish());
        vm.SetAddDlcHandler(parentSymbol => GetMainWindowViewModel()?.NavigateToPublish(parentSymbol));
        vm.SetEditHandler(ShowEditDialogAsync);
        vm.SetConfirmDeleteHandler(ConfirmDeleteAsync);
    }

    private static MainWindowViewModel? GetMainWindowViewModel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.DataContext is MainWindowViewModel main)
        {
            return main;
        }

        return null;
    }

    private async void Refresh_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private void AddGame_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm)
        {
            vm.AddGameCommand.Execute(null);
        }
    }

    private void AddDlc_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm
            && sender is Control { Tag: StudioContentItem item })
        {
            vm.AddDlcCommand.Execute(item);
        }
    }

    private async void Edit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm
            && sender is Control { Tag: StudioContentItem item })
        {
            await vm.EditCommand.ExecuteAsync(item);
        }
    }

    private async void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StudioViewModel vm
            && sender is Control { Tag: StudioContentItem item })
        {
            await vm.DeleteCommand.ExecuteAsync(item);
        }
    }

    private async Task<bool> ShowEditDialogAsync(StudioContentItem item)
    {
        var genreOptions = SgsGameTaxonomy.Genres.Select(g => g.Symbol).Prepend("").ToArray();
        var engineOptions = SgsGameTaxonomy.Engines.Select(e => e.Symbol).Prepend("").ToArray();

        var titleBox = new TextBox { Text = item.Title, PlaceholderText = "np. Demo Runner" };
        var descBox = new TextBox
        {
            Text = item.Description ?? string.Empty,
            PlaceholderText = "Krótki opis widoczny w sklepie",
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 88
        };
        var hardwareBox = new TextBox
        {
            Text = item.HardwareRequirements ?? string.Empty,
            PlaceholderText = "opcjonalnie",
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 56
        };
        var otherBox = new TextBox
        {
            Text = item.OtherInformation ?? string.Empty,
            PlaceholderText = "opcjonalnie",
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 56
        };
        var genreCombo = new ComboBox
        {
            ItemsSource = genreOptions,
            SelectedItem = genreOptions.Contains(item.GenreSymbol ?? string.Empty)
                ? item.GenreSymbol ?? string.Empty
                : string.Empty,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        var engineCombo = new ComboBox
        {
            ItemsSource = engineOptions,
            SelectedItem = engineOptions.Contains(item.EngineSymbol ?? string.Empty)
                ? item.EngineSymbol ?? string.Empty
                : string.Empty,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        var draftBox = new CheckBox
        {
            Content = "Szkic (ukryty w sklepie)",
            IsChecked = item.DraftP
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

        var dialog = new FAContentDialog
        {
            Title = item.IsDlc ? $"Edytuj DLC — {item.Symbol}" : $"Edytuj grę — {item.Symbol}",
            PrimaryButtonText = "Zapisz",
            CloseButtonText = "Anuluj",
            DefaultButton = FAContentDialogButton.Primary,
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 520
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
        return true;
    }

    private async Task<bool> ConfirmDeleteAsync(StudioContentItem item)
    {
        var dialog = new FAContentDialog
        {
            Title = "Usuń treść",
            Content = $"Czy na pewno usunąć „{item.Title}”? Tej operacji nie można cofnąć.",
            PrimaryButtonText = "Usuń",
            CloseButtonText = "Anuluj",
            DefaultButton = FAContentDialogButton.Close
        };

        var result = await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        return result == FAContentDialogResult.Primary;
    }
}
