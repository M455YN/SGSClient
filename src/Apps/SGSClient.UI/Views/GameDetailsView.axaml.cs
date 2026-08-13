using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.Core.Models;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class GameDetailsView : UserControl
{
    public GameDetailsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is GameDetailsViewModel vm)
            {
                vm.SetOpenPropertiesHandler(OpenProperties);
            }
        };
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

    private async void Launch_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            await vm.LaunchCommand.ExecuteAsync(null);
        }
    }

    private async void Install_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            await vm.InstallCommand.ExecuteAsync(null);
        }
    }

    private async void Uninstall_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            await vm.UninstallCommand.ExecuteAsync(null);
        }
    }

    private void Properties_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            vm.OpenPropertiesCommand.Execute(null);
        }
    }

    private void Tab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm && sender is Control { Tag: string tab })
        {
            vm.SelectTabCommand.Execute(tab);
        }
    }

    private void OpenProperties(GameLibraryItem item)
    {
        var window = new GamePropertiesWindow();
        window.SetGame(item.GameId, item.BuildId, item.Title);
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
