using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Enums;
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

/// <summary>Wattpad-like story browser with an in-app reader for .txt and .md files.</summary>
public sealed partial class CreativeStoriesViewModel(ISgsApiService? api, string apiBaseUrl)
    : CreativeSectionViewModel(api, apiBaseUrl, GameContentKind.Book)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
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

    public async Task OpenAsync(CreativeItem item)
    {
        if (item.Extension is not (".txt" or ".md"))
        {
            // Binary formats (pdf, epub, docx...) are not parsed; the system viewer handles them.
            StatusText = ReaderStatus = Loc.T("Ten format otwiera się w zewnętrznej aplikacji");
            try
            {
                Process.Start(new ProcessStartInfo(item.FileUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusText = ReaderStatus = Loc.F("Nie udało się otworzyć pliku: {0}", ex.Message);
            }

            return;
        }

        _readingItem = item;
        ReaderTitle = item.Title;
        ReaderStatus = Loc.T("Wczytywanie…");
        Chapters.Clear();
        ChapterText = string.Empty;
        SelectedChapterIndex = -1;
        IsReading = true;

        try
        {
            var text = await Http.GetStringAsync(item.FileUrl);
            if (_readingItem != item)
            {
                return; // the reader was closed or another story opened while downloading
            }

            foreach (var chapter in SplitChapters(text, item.Title))
            {
                Chapters.Add(chapter);
            }

            ReaderStatus = string.Empty;
            SelectedChapterIndex = _lastChapterByItem.TryGetValue(item.Id, out var last)
                ? Math.Min(last, Chapters.Count - 1)
                : 0;
        }
        catch (HttpRequestException)
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

    /// <summary>Splits at "# " / "## " headings; a text without headings becomes one chapter.</summary>
    private static List<StoryChapter> SplitChapters(string text, string storyTitle)
    {
        var chapters = new List<StoryChapter>();
        var title = storyTitle;
        var body = new StringBuilder();
        var started = false;

        void Flush()
        {
            var content = body.ToString().Trim();
            if (started || content.Length > 0)
            {
                chapters.Add(new StoryChapter { Title = title, Text = content });
            }

            body.Clear();
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("# ") || trimmed.StartsWith("## "))
            {
                Flush();
                title = trimmed.TrimStart('#').Trim();
                started = true;
                continue;
            }

            body.AppendLine(trimmed);
        }

        Flush();
        if (chapters.Count == 0)
        {
            chapters.Add(new StoryChapter { Title = storyTitle, Text = string.Empty });
        }

        return chapters;
    }
}
