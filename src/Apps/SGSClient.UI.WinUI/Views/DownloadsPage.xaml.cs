using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class DownloadsPage : Page
{
    private bool _suppressBandwidth;

    public DownloadsViewModel ViewModel { get; }

    public DownloadsPage()
    {
        ViewModel = App.Services.GetRequiredService<DownloadsViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        // Bound once: the collection raises its own change events and rows update through their bindings.
        DownloadsList.ItemsSource = ViewModel.UpNext;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DownloadsViewModel.DownloadMaxMbps))
            {
                SyncBandwidthBox();
            }
        };
        SyncBandwidthBox();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Refresh();
        SyncBandwidthBox();
    }

    private void SyncBandwidthBox()
    {
        _suppressBandwidth = true;
        try
        {
            BandwidthBox.Value = ViewModel.DownloadMaxMbps;
        }
        finally
        {
            _suppressBandwidth = false;
        }
    }

    private void BandwidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressBandwidth || double.IsNaN(args.NewValue))
        {
            return;
        }

        ViewModel.DownloadMaxMbps = args.NewValue;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => ViewModel.Refresh();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.CancelCommand.Execute(row);
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.PauseCommand.Execute(row);
        }
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.ResumeCommand.Execute(row);
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.MoveUpCommand.Execute(row);
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.MoveDownCommand.Execute(row);
        }
    }

    private void DownloadsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DownloadRowItem row)
        {
            ViewModel.OpenDetailsCommand.Execute(row);
        }
    }
}
