using Avalonia.Controls;
using Avalonia.Interactivity;
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

        var item = (sender as Control)?.Tag as GameListItem;
        await vm.AddToLibraryCommand.ExecuteAsync(item);
    }
}
