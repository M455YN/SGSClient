using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class CreativeArtSection : UserControl
{
    private CreativeArtViewModel? _viewModel;

    public CreativeArtSection()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Categories.CollectionChanged -= OnCategoriesChanged;
        }

        _viewModel = DataContext as CreativeArtViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.Categories.CollectionChanged += OnCategoriesChanged;
        }

        RebuildChips();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CreativeArtViewModel.SelectedItem))
        {
            DetailScroll.Offset = default;
        }

        RebuildChips();
    }

    private void OnCategoriesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        RebuildChips();

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
            var chip = new Button { Content = category };
            chip.Classes.Add("chip");
            if (category == vm.SelectedCategory)
            {
                chip.Classes.Add("selected");
            }

            chip.Click += (_, _) => vm.SelectCategoryCommand.Execute(category);
            ChipsPanel.Children.Add(chip);
        }
    }

    private async void SearchBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not CreativeArtViewModel vm || sender is not TextBox box)
        {
            return;
        }

        vm.SearchText = box.Text ?? string.Empty;
        await vm.LoadAsync();
    }

    private void Tile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is CreativeArtViewModel vm && (sender as Control)?.Tag is CreativeItem item)
        {
            vm.OpenCommand.Execute(item);
        }
    }
}
