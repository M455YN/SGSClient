using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class MainWindow : Window
{
    private bool _suppressNavCallback;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(MainWindowViewModel.SelectedNavTag))
                    {
                        SyncNavSelection(vm.SelectedNavTag);
                    }
                };
            }
        };
    }

    private void SyncNavSelection(string tag)
    {
        FANavigationViewItem? match = null;
        foreach (var item in NavView.MenuItems.OfType<FANavigationViewItem>()
                     .Concat(NavView.FooterMenuItems.OfType<FANavigationViewItem>()))
        {
            if (item.Tag as string == tag)
            {
                match = item;
                break;
            }
        }

        if (match is null || ReferenceEquals(NavView.SelectedItem, match))
        {
            return;
        }

        _suppressNavCallback = true;
        try
        {
            NavView.SelectedItem = match;
        }
        finally
        {
            _suppressNavCallback = false;
        }
    }

    private void NavView_OnSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs e)
    {
        if (_suppressNavCallback || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.SelectedItem is FANavigationViewItem item && item.Tag is string tag)
        {
            vm.NavigateCommand.Execute(tag);
        }
    }
}
