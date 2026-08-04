using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            if (DataContext is LibraryViewModel vm)
            {
                await vm.LoadAsync();
            }
        };
    }

    private async void Refresh_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }

    private async void Remove_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm)
        {
            await vm.RemoveCommand.ExecuteAsync((sender as Control)?.Tag as LibraryGameItem);
        }
    }

    private async void Launch_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm)
        {
            await vm.LaunchCommand.ExecuteAsync((sender as Control)?.Tag as LibraryGameItem);
        }
    }
}
