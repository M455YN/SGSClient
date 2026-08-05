using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            if (DataContext is HomeViewModel vm)
            {
                await vm.EnsureLoadedAsync();
            }
        };
    }

    private void OpenStore_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            vm.OpenStoreCommand.Execute(null);
        }
    }

    private void OpenLibrary_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            vm.OpenLibraryCommand.Execute(null);
        }
    }

    private void Featured_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is HomeViewModel vm && (sender as Control)?.Tag is HomeFeaturedItem item)
        {
            vm.OpenFeaturedCommand.Execute(item);
        }
    }
}
