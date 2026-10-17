using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SGSClient.UI.ViewModels;
using Windows.System;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class CreativeStoriesSection : UserControl
{
    private CreativeStoriesViewModel? _viewModel;
    private bool _syncingChapter;
    private int _shownChapter = -1;

    public CreativeStoriesSection()
    {
        InitializeComponent();
    }

    /// <summary>Assigned by <see cref="CreativePage"/> right after construction.</summary>
    public CreativeStoriesViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            CommentsControl.ViewModel = value?.Comments;
            if (value is not null)
            {
                value.PropertyChanged += (_, _) => RefreshUi();
                value.Items.CollectionChanged += (_, _) => RefreshItems();
                value.Chapters.CollectionChanged += (_, _) => RefreshChapters();
            }

            RefreshItems();
            RefreshChapters();
            RefreshUi();
        }
    }

    private void RefreshItems()
    {
        if (_viewModel is null)
        {
            return;
        }

        ItemsList.ItemsSource = _viewModel.Items.ToList();
    }

    private void RefreshChapters()
    {
        if (_viewModel is null)
        {
            return;
        }

        _syncingChapter = true;
        ChapterPicker.ItemsSource = _viewModel.Chapters.ToList();
        _syncingChapter = false;
    }

    private void RefreshUi()
    {
        if (_viewModel is null)
        {
            return;
        }

        var vm = _viewModel;
        StatusText.Text = vm.StatusText;
        BusyRing.IsActive = vm.IsBusy;
        BrowsePanel.Visibility = vm.IsReading ? Visibility.Collapsed : Visibility.Visible;
        ReaderPanel.Visibility = vm.IsReading ? Visibility.Visible : Visibility.Collapsed;

        ReaderTitleText.Text = vm.ReaderTitle;
        ReaderStatusText.Text = vm.ReaderStatus;
        ChapterTextBlock.Text = vm.ChapterText;
        PrevButton.IsEnabled = vm.HasPreviousChapter;
        NextButton.IsEnabled = vm.HasNextChapter;

        if (ChapterPicker.SelectedIndex != vm.SelectedChapterIndex)
        {
            _syncingChapter = true;
            ChapterPicker.SelectedIndex = vm.SelectedChapterIndex;
            _syncingChapter = false;
        }

        if (vm.SelectedChapterIndex != _shownChapter)
        {
            _shownChapter = vm.SelectedChapterIndex;
            ReaderScroll.ChangeView(null, 0, null);
        }
    }

    private void Card_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is FrameworkElement { DataContext: CreativeItem item })
        {
            _ = _viewModel.OpenAsync(item);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _viewModel?.CloseCommand.Execute(null);

    private void Prev_Click(object sender, RoutedEventArgs e) => _viewModel?.PreviousChapterCommand.Execute(null);

    private void Next_Click(object sender, RoutedEventArgs e) => _viewModel?.NextChapterCommand.Execute(null);

    private void ChapterPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingChapter || _viewModel is null || ChapterPicker.SelectedIndex < 0)
        {
            return;
        }

        _viewModel.SelectedChapterIndex = ChapterPicker.SelectedIndex;
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || _viewModel is null)
        {
            return;
        }

        _viewModel.SearchText = SearchBox.Text;
        _ = _viewModel.LoadAsync();
    }
}
