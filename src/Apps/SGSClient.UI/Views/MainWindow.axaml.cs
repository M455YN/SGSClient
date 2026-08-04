using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void NavView_OnSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.SelectedItem is FANavigationViewItem item && item.Tag is string tag)
        {
            vm.NavigateCommand.Execute(tag);
        }
    }
}
