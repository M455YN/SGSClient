using Avalonia.Controls;
using Avalonia.Input;
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
                vm.SetOpenPropertiesHandler(OpenProperties);
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

    private void Home_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm)
        {
            vm.SelectHomeCommand.Execute(null);
        }
    }

    private void Sidebar_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm && sender is ListBox { SelectedItem: LibraryGameItem item })
        {
            vm.SelectGameCommand.Execute(item);
        }
    }

    private void Poster_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm
            && (sender as Control)?.Tag is LibraryGameItem item)
        {
            vm.SelectGameCommand.Execute(item);
        }
    }

    private async void Launch_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm)
        {
            e.Handled = true;
            await vm.LaunchCommand.ExecuteAsync((sender as Control)?.Tag as LibraryGameItem);
        }
    }

    private void OpenProperties(LibraryGameItem item)
    {
        var window = new GamePropertiesWindow();
        window.ViewModel.ViewInStoreRequested += (_, _) =>
        {
            window.Close();
            if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel main)
            {
                main.OpenGameDetails(item.GameId, "Library");
            }
        };
        window.SetGame(item.GameId, item.BuildId, item.Title);
        window.Closed += async (_, _) =>
        {
            if (DataContext is LibraryViewModel vm)
            {
                await vm.LoadAsync();
            }
        };
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }
}
