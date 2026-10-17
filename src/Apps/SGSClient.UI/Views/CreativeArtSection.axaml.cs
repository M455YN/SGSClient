using Avalonia.Controls;
using Avalonia.Input;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class CreativeArtSection : UserControl
{
    public CreativeArtSection()
    {
        InitializeComponent();
    }

    private async void SearchBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not CreativeArtViewModel vm || sender is not TextBox box)
        {
            return;
        }

        vm.SearchText = box.Text ?? string.Empty;
        await vm.LoadAsync();
    }

    private void Tile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is CreativeArtViewModel vm && (sender as Control)?.Tag is CreativeItem item)
        {
            vm.OpenCommand.Execute(item);
        }
    }
}
