using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class GameDetailsView : UserControl
{
    public GameDetailsView()
    {
        InitializeComponent();
    }

    private void Back_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            vm.CloseCommand.Execute(null);
        }
    }

    private async void AddToLibrary_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            await vm.AddToLibraryCommand.ExecuteAsync(null);
        }
    }

    private void Tab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm && sender is Control { Tag: string tab })
        {
            vm.SelectTabCommand.Execute(tab);
        }
    }
}
