using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class CreativeStoriesSection : UserControl
{
    public CreativeStoriesSection()
    {
        InitializeComponent();
    }

    private void Card_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreativeStoriesViewModel vm && sender is Control { DataContext: CreativeItem item })
        {
            _ = vm.OpenAsync(item);
        }
    }

    private async void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not CreativeStoriesViewModel vm)
        {
            return;
        }

        await vm.LoadAsync(); // SearchText is already bound two-way
    }
}
