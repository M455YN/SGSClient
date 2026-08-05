using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class GamesView : UserControl
{
    public GamesView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            if (DataContext is GamesViewModel vm)
            {
                await vm.EnsureLoadedAsync();
            }
        };
    }

    private async void Refresh_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GamesViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private async void AddToLibrary_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GamesViewModel vm)
        {
            return;
        }

        e.Handled = true;
        var item = (sender as Control)?.Tag as GameListItem;
        await vm.AddToLibraryCommand.ExecuteAsync(item);
    }

    private void GameCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not GamesViewModel vm)
        {
            return;
        }

        if (e.Source is Control source && source.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        if ((sender as Control)?.Tag is GameListItem item)
        {
            vm.OpenDetailsCommand.Execute(item);
        }
    }
}
