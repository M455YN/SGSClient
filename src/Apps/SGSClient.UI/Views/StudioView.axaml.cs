using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
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
        var titleBox = new TextBox
        {
            Text = item.Title,
            PlaceholderText = "Tytuł"
        };
        var descBox = new TextBox
        {
            Text = item.Description ?? string.Empty,
            PlaceholderText = "Opis",
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 96
        };
        var draftBox = new CheckBox
        {
            Content = "Szkic (ukryty w sklepie)",
            IsChecked = item.DraftP
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Tytuł", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(titleBox);
        panel.Children.Add(new TextBlock { Text = "Opis", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(descBox);
        panel.Children.Add(draftBox);

        var dialog = new FAContentDialog
        {
            Title = item.IsDlc ? $"Edytuj DLC — {item.Symbol}" : $"Edytuj grę — {item.Symbol}",
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
