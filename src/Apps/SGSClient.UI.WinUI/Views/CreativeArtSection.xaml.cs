using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.ViewModels;
using Windows.System;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class CreativeArtSection : UserControl
{
    private CreativeArtViewModel? _viewModel;

    public CreativeArtSection()
    {
        InitializeComponent();
    }

    /// <summary>Assigned by <see cref="CreativePage"/> right after construction.</summary>
    public CreativeArtViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            if (value is not null)
            {
                value.PropertyChanged += (_, _) => RefreshUi();
                value.Items.CollectionChanged += (_, _) => RefreshUi();
            }

            RefreshUi();
        }
    }

    private void RefreshUi()
    {
        if (_viewModel is null)
        {
            return;
        }

        StatusText.Text = _viewModel.StatusText;
        ItemsList.ItemsSource = _viewModel.Items.ToList();
        Lightbox.Visibility = _viewModel.IsLightboxOpen ? Visibility.Visible : Visibility.Collapsed;
        LightboxPanel.DataContext = _viewModel.SelectedItem;
        LightboxTags.Text = _viewModel.SelectedTagsText;
    }

    private async void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || _viewModel is null)
        {
            return;
        }

        _viewModel.SearchText = SearchBox.Text;
        await _viewModel.LoadAsync();
    }

    private void Tile_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (_viewModel is not null && sender is FrameworkElement { DataContext: CreativeItem item })
        {
            _viewModel.OpenCommand.Execute(item);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => _viewModel?.CloseCommand.Execute(null);

    private void Download_Click(object sender, RoutedEventArgs e) => _viewModel?.DownloadCommand.Execute(null);
}
