using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class LobbyView : UserControl
{
    public LobbyView()
    {
        InitializeComponent();
    }

    private async void CopyCode_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LobbyViewModel { Code.Length: > 0 } vm
            && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(vm.Code);
        }
    }
}
