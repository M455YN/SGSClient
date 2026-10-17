using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>One chapter of a story; the heading becomes the title.</summary>
public sealed class StoryChapter
{
    public required string Title { get; init; }
    public string Text { get; init; } = string.Empty;

    public override string ToString() => Title;
}

/// <summary>Wattpad-like story browser with an in-app chapter reader.</summary>
public sealed partial class CreativeStoriesViewModel(ISgsApiService? api, string apiBaseUrl)
    : CreativeSectionViewModel(api, apiBaseUrl, "stories")
{
    private readonly Dictionary<int, int> _lastChapterByItem = [];
    private CreativeItem? _readingItem;

    [ObservableProperty] public partial bool IsReading { get; set; }
    [ObservableProperty] public partial string ReaderTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string ReaderStatus { get; set; } = string.Empty;
    [ObservableProperty] public partial int SelectedChapterIndex { get; set; } = -1;
    [ObservableProperty] public partial string ChapterText { get; set; } = string.Empty;

    public ObservableCollection<StoryChapter> Chapters { get; } = [];

    public bool HasPreviousChapter => SelectedChapterIndex > 0;
    public bool HasNextChapter => SelectedChapterIndex >= 0 && SelectedChapterIndex < Chapters.Count - 1;

    protected override async Task<IEnumerable<CreativeItem>> FetchAsync(CreativeQuery query) =>
        (await Api!.GetStoriesAsync(query)).Select(x => CreativeItem.FromStory(x, ApiBaseUrl));

    public async Task OpenAsync(CreativeItem item)
    {
        if (Api is null)
        {
            return;
        }

        _readingItem = item;
        ReaderTitle = item.Title;
        ReaderStatus = Loc.T("Wczytywanie…");
        Chapters.Clear();
        ChapterText = string.Empty;
        SelectedChapterIndex = -1;
        IsReading = true;
        _ = Comments.LoadAsync(item.Id);

        try
        {
            var story = await Api.GetStoryAsync(item.Id);
            if (_readingItem != item)
            {
                return; // the reader was closed or another story opened while loading
            }

            if (story is null)
            {
                ReaderStatus = Loc.T("Nie udało się pobrać opowiadania.");
                return;
            }

            foreach (var chapter in story.Chapters.OrderBy(c => c.Index))
            {
                Chapters.Add(new StoryChapter { Title = chapter.Title, Text = chapter.Text });
            }

            ReaderStatus = string.Empty;
            SelectedChapterIndex = Chapters.Count == 0
                ? -1
                : _lastChapterByItem.TryGetValue(item.Id, out var last) ? Math.Min(last, Chapters.Count - 1) : 0;
        }
        catch (Exception)
        {
            if (_readingItem == item)
            {
                ReaderStatus = Loc.T("Nie udało się pobrać opowiadania.");
            }
        }
    }

    [RelayCommand]
    private void Close()
    {
        _readingItem = null;
        IsReading = false;
        Comments.Clear();
        SelectedChapterIndex = -1;
        Chapters.Clear();
        ChapterText = string.Empty;
        ReaderStatus = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(HasPreviousChapter))]
    private void PreviousChapter() => SelectedChapterIndex--;

    [RelayCommand(CanExecute = nameof(HasNextChapter))]
    private void NextChapter() => SelectedChapterIndex++;

    partial void OnSelectedChapterIndexChanged(int value)
    {
        ChapterText = value >= 0 && value < Chapters.Count ? Chapters[value].Text : string.Empty;
        if (_readingItem is not null && value >= 0)
        {
            _lastChapterByItem[_readingItem.Id] = value;
        }

        OnPropertyChanged(nameof(HasPreviousChapter));
        OnPropertyChanged(nameof(HasNextChapter));
        PreviousChapterCommand.NotifyCanExecuteChanged();
        NextChapterCommand.NotifyCanExecuteChanged();
    }
}
