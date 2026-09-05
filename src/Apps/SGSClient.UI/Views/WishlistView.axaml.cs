using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class WishlistView : UserControl
{
    public WishlistView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            if (DataContext is WishlistViewModel vm)
            {
                await vm.LoadAsync();
            }
        };
    }

    private async void AddToLibrary_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WishlistViewModel vm)
        {
            return;
        }

        if ((sender as Control)?.Tag is GameListItem item)
        {
            await vm.AddToLibraryCommand.ExecuteAsync(item);
        }
    }

    private void GameCard_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && DataContext is WishlistViewModel vm
            && (sender as Control)?.Tag is GameListItem item)
        {
            vm.OpenDetailsCommand.Execute(item);
        }
    }
}
