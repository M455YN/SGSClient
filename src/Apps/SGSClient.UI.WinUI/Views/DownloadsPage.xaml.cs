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
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DownloadsViewModel.DownloadMaxMbps))
            {
                SyncBandwidthBox();
            }

            RefreshUi();
        };
        ViewModel.Items.CollectionChanged += (_, _) => RefreshUi();
        SyncBandwidthBox();
        RefreshUi();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Refresh();
        SyncBandwidthBox();
        RefreshUi();
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

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        DownloadsList.Visibility = ViewModel.HasItems ? Visibility.Visible : Visibility.Collapsed;
        DownloadsList.ItemsSource = null;
        DownloadsList.ItemsSource = ViewModel.Items;
    }

    private void BandwidthBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressBandwidth || double.IsNaN(args.NewValue))
        {
            return;
        }

        ViewModel.DownloadMaxMbps = args.NewValue;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Refresh();
        RefreshUi();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.CancelCommand.Execute(row);
            RefreshUi();
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.PauseCommand.Execute(row);
            RefreshUi();
        }
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.ResumeCommand.Execute(row);
            RefreshUi();
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.MoveUpCommand.Execute(row);
            RefreshUi();
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DownloadRowItem row })
        {
            ViewModel.MoveDownCommand.Execute(row);
            RefreshUi();
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
