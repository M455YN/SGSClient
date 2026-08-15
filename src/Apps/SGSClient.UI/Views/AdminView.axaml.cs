using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using SGSClient.Core.Packaging;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class AdminView : UserControl
{
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
        var draftBox = new CheckBox { Content = "Szkic (ukryty w sklepie)", IsChecked = item.DraftP };
        var featuredBox = new CheckBox { Content = "Wyróżnione", IsChecked = item.FeaturedP };

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

        var dialog = new FAContentDialog
        {
            Title = $"Admin — {item.Symbol}",
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
        item.EditFeatured = featuredBox.IsChecked == true;
        return true;
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
