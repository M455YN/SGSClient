using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>Comment list + input for one work; bind <c>DataContext</c> to a <see cref="CreativeCommentsViewModel"/>.</summary>
public partial class CreativeComments : UserControl
{
    public CreativeComments()
    {
        InitializeComponent();
    }

    private void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreativeCommentsViewModel vm && sender is Control { DataContext: CreativeCommentItem item })
        {
            vm.DeleteCommand.Execute(item);
        }
    }

    private void Input_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && DataContext is CreativeCommentsViewModel vm && vm.PostCommand.CanExecute(null))
        {
            vm.PostCommand.Execute(null);
            e.Handled = true;
        }
    }
}
