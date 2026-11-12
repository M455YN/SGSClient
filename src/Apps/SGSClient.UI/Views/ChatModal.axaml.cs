using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>Body of the conversation dialog; the dialog itself is created by the friends view.</summary>
public partial class ChatModal : UserControl
{
    public ChatModal()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ChatModalViewModel vm)
            {
                vm.Messages.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(Scroller.ScrollToEnd);
                DraftBox.AttachedToVisualTree += (_, _) => DraftBox.Focus();
            }
        };
    }

    private void Draft_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChatModalViewModel vm)
        {
            e.Handled = true;
            vm.SendCommand.Execute(null);
        }
    }
}
