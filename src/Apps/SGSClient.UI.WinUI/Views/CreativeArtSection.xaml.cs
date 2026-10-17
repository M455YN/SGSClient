using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
            CommentsControl.ViewModel = value?.Comments;
            if (value is not null)
            {
                value.PropertyChanged += (_, _) =>
                {
                    RefreshUi();
                    RebuildChips();
                };
                value.Items.CollectionChanged += (_, _) => RefreshUi();
                value.VisibleItems.CollectionChanged += (_, _) => RefreshUi();
                value.Categories.CollectionChanged += (_, _) => RebuildChips();
            }

            RefreshUi();
            RebuildChips();
        }
    }

    private void RefreshUi()
    {
        var vm = _viewModel;
        if (vm is null)
        {
            return;
        }

        StatusText.Text = vm.StatusText;
        BusyRing.IsActive = vm.IsBusy;
        if (!ReferenceEquals(ItemsList.ItemsSource, vm.VisibleItems))
        {
            ItemsList.ItemsSource = vm.VisibleItems;
        }

        GalleryPanel.Visibility = ToVisibility(vm.ShowGallery);
        DetailPanel.Visibility = ToVisibility(!vm.ShowGallery);
        if (!ReferenceEquals(DetailPanel.DataContext, vm.SelectedItem))
        {
            DetailPanel.DataContext = vm.SelectedItem;
            DetailPanel.ChangeView(null, 0, null);
        }
    }

    private void RebuildChips()
    {
        ChipsPanel.Children.Clear();
        var vm = _viewModel;
        if (vm is null)
        {
            return;
        }

        foreach (var category in vm.Categories)
        {
            var chip = new Button
            {
                Content = category,
                Style = (Style)Resources[category == vm.SelectedCategory ? "ChipSelectedStyle" : "ChipStyle"]
            };
            chip.Click += (_, _) => vm.SelectCategoryCommand.Execute(category);
            ChipsPanel.Children.Add(chip);
        }
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || _viewModel is null)
        {
            return;
        }

        _viewModel.SearchText = SearchBox.Text;
        await _viewModel.LoadAsync();
    }

    private void Gallery_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_viewModel is not null && e.ClickedItem is CreativeItem item)
        {
            _viewModel.OpenCommand.Execute(item);
        }
    }

    private void Tile_PointerEntered(object sender, PointerRoutedEventArgs e) =>
        SetOverlayVisibility(sender, Visibility.Visible);

    private void Tile_PointerExited(object sender, PointerRoutedEventArgs e) =>
        SetOverlayVisibility(sender, Visibility.Collapsed);

    private static void SetOverlayVisibility(object sender, Visibility visibility)
    {
        if (sender is Grid tile && tile.Children.OfType<Border>().FirstOrDefault(b => b.Tag as string == "overlay") is { } overlay)
        {
            overlay.Visibility = visibility;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => _viewModel?.CloseCommand.Execute(null);

    private void Download_Click(object sender, RoutedEventArgs e) => _viewModel?.DownloadCommand.Execute(null);
}
