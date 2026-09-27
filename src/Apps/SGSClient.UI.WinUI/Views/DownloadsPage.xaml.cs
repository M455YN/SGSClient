using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class DownloadsPage : Page
{
    public DownloadsViewModel ViewModel { get; }

    public DownloadsPage()
    {
        ViewModel = App.Services.GetRequiredService<DownloadsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        ViewModel.Items.CollectionChanged += (_, _) => RefreshUi();
        RefreshUi();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Refresh();
        RefreshUi();
    }

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        DownloadsList.Visibility = ViewModel.HasItems ? Visibility.Visible : Visibility.Collapsed;
        DownloadsList.ItemsSource = null;
        DownloadsList.ItemsSource = ViewModel.Items;
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

    private void DownloadsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is DownloadRowItem row)
        {
            ViewModel.OpenDetailsCommand.Execute(row);
        }
    }
}
