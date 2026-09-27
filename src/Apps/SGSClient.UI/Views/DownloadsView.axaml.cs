using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class DownloadsView : UserControl
{
    public DownloadsView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is DownloadsViewModel vm)
            {
                vm.Refresh();
            }
        };
    }

    private void Item_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: DownloadRowItem row }
            && DataContext is DownloadsViewModel vm)
        {
            vm.OpenDetailsCommand.Execute(row);
        }
    }
}
