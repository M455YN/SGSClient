using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SGSClient.UI.ViewModels;
using Windows.System;
using Windows.UI.Core;
using Microsoft.UI.Input;

namespace SGSClient.UI.WinUI.Views;

/// <summary>Comment list + input for one work; the owner section assigns <see cref="ViewModel"/>.</summary>
public sealed partial class CreativeComments : UserControl
{
    public CreativeComments()
    {
        InitializeComponent();
    }

    public CreativeCommentsViewModel? ViewModel
    {
        get => DataContext as CreativeCommentsViewModel;
        set => DataContext = value;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm && sender is FrameworkElement { DataContext: CreativeCommentItem item })
        {
            vm.DeleteCommand.Execute(item);
        }
    }

    private void Input_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter
            && InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down)
            && ViewModel is { } vm && vm.PostCommand.CanExecute(null))
        {
            vm.PostCommand.Execute(null);
            e.Handled = true;
        }
    }
}
