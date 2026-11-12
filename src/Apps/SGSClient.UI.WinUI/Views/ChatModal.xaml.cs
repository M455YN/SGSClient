using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SGSClient.UI.ViewModels;
using Windows.System;

namespace SGSClient.UI.WinUI.Views;

/// <summary>Body of the conversation dialog; the dialog itself is created by the friends page.</summary>
public sealed partial class ChatModal : UserControl
{
    public ChatModal()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ChatModalViewModel vm)
            {
                vm.Messages.CollectionChanged += (_, _) => ScrollToEnd();
                DraftBox.Loaded += (_, _) => DraftBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            }
        };
    }

    private void ScrollToEnd() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            Scroller.UpdateLayout();
            Scroller.ChangeView(null, Scroller.ScrollableHeight, null, disableAnimation: true);
        });

    private void Draft_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && DataContext is ChatModalViewModel vm)
        {
            e.Handled = true;
            vm.SendCommand.Execute(null);
        }
    }
}
