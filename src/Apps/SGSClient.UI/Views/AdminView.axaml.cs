using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
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
        var titleBox = new TextBox { Text = item.Title, PlaceholderText = "Tytuł" };
        var descBox = new TextBox
        {
            Text = item.Description ?? string.Empty,
            PlaceholderText = "Opis",
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 96
        };
        var draftBox = new CheckBox { Content = "Szkic (ukryty w sklepie)", IsChecked = item.DraftP };
        var featuredBox = new CheckBox { Content = "Wyróżnione", IsChecked = item.FeaturedP };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Tytuł", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(titleBox);
        panel.Children.Add(new TextBlock { Text = "Opis", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(descBox);
        panel.Children.Add(draftBox);
        panel.Children.Add(featuredBox);

        var dialog = new FAContentDialog
        {
            Title = $"Admin — {item.Symbol}",
            PrimaryButtonText = "Zapisz",
            CloseButtonText = "Anuluj",
            DefaultButton = FAContentDialogButton.Primary,
            Content = panel
        };

        var result = await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        if (result != FAContentDialogResult.Primary)
        {
            return false;
        }

        item.EditTitle = titleBox.Text ?? string.Empty;
        item.EditDescription = descBox.Text;
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
