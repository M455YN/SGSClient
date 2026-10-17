using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>A chapter being written.</summary>
public sealed partial class ChapterDraft : ObservableObject
{
    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
    [ObservableProperty] public partial string Text { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(Title) ? Loc.T("Rozdział bez tytułu") : Title;
}

/// <summary>"Dodaj opowiadanie": write chapters in the app (Wattpad style) or import a .txt/.md file.</summary>
public sealed partial class AddStoryViewModel(ISgsApiService? api, IAuthSession? auth) : ObservableObject
{
    public ObservableCollection<ChapterDraft> Chapters { get; } = [];
    public ObservableCollection<string> Issues { get; } = [];

    [ObservableProperty] public partial string StoryTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string Description { get; set; } = string.Empty;
    [ObservableProperty] public partial string Tags { get; set; } = string.Empty;
    [ObservableProperty] public partial string CoverPath { get; set; } = string.Empty;
    [ObservableProperty] public partial ChapterDraft? SelectedChapter { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string AuthHint { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }

    public string PageTitle => Loc.T("Dodaj opowiadanie");

    public void Prepare()
    {
        if (Chapters.Count == 0)
        {
            AddChapter();
        }

        StatusText = string.Empty;
        Issues.Clear();
        RefreshAuthHint();
    }

    public void RefreshAuthHint() =>
        AuthHint = auth?.IsAvailable != true
            ? Loc.T("Logowanie jest wyłączone — publikacja nastąpi bez konta.")
            : auth.IsLoggedIn
                ? $"Zalogowano jako {auth.CurrentUser?.DisplayName ?? "gracz"}."
                : Loc.T("Zaloguj się w Profilu przed publikacją.");

    [RelayCommand]
    private void AddChapter()
    {
        var chapter = new ChapterDraft { Title = Loc.F("Rozdział {0}", Chapters.Count + 1) };
        Chapters.Add(chapter);
        SelectedChapter = chapter;
    }

    [RelayCommand]
    private void RemoveChapter(ChapterDraft? chapter)
    {
        if (chapter is null || IsBusy)
        {
            return;
        }

        var index = Chapters.IndexOf(chapter);
        Chapters.Remove(chapter);
        if (Chapters.Count == 0)
        {
            AddChapter();
        }
        else if (SelectedChapter == chapter || SelectedChapter is null)
        {
            SelectedChapter = Chapters[Math.Min(index, Chapters.Count - 1)];
        }
    }

    /// <summary>Replaces the chapters with the text of a .txt/.md file split at "# " / "## " headings.</summary>
    public async Task ImportFileAsync(string path)
    {
        try
        {
            var text = await File.ReadAllTextAsync(path);
            var parsed = SplitIntoChapters(text, Path.GetFileNameWithoutExtension(path));
            Chapters.Clear();
            foreach (var (title, body) in parsed)
            {
                Chapters.Add(new ChapterDraft { Title = title, Text = body });
            }

            SelectedChapter = Chapters.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(StoryTitle))
            {
                StoryTitle = Path.GetFileNameWithoutExtension(path).Replace('_', ' ').Replace('-', ' ').Trim();
            }

            StatusText = Loc.F("Zaimportowano rozdziałów: {0}.", Chapters.Count);
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się zaimportować pliku: {0}", ex.Message);
        }
    }

    [RelayCommand]
    private async Task PublishAsync()
    {
        if (api is null || IsBusy)
        {
            return;
        }

        var written = Chapters.Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();
        if (string.IsNullOrWhiteSpace(StoryTitle) || written.Count == 0)
        {
            StatusText = Loc.T("Podaj tytuł i napisz przynajmniej jeden rozdział.");
            return;
        }

        RefreshAuthHint();
        IsBusy = true;
        Issues.Clear();
        StatusText = Loc.T("Wysyłanie do sklepu…");
        try
        {
            var (story, errors) = await api.CreateStoryAsync(
                new CreateStoryRequestDto
                {
                    Title = StoryTitle.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    Tags = AddMediaViewModel.ParseTags(Tags),
                    Chapters = written
                        .Select(c => new CreateStoryChapterDto
                        {
                            Title = string.IsNullOrWhiteSpace(c.Title) ? Loc.T("Rozdział bez tytułu") : c.Title.Trim(),
                            Text = c.Text.Trim()
                        })
                        .ToList()
                },
                string.IsNullOrWhiteSpace(CoverPath) ? null : CoverPath);
            if (story is not null && errors.Count == 0)
            {
                StatusText = Loc.F("Opublikowano: {0}. Znajdziesz to w Twórczości.", story.Title);
                ResetForm();
            }
            else
            {
                foreach (var error in errors)
                {
                    Issues.Add(error);
                }

                if (story is not null)
                {
                    // created without its cover: keep the form from being published a second time
                    ResetForm();
                    StatusText = Loc.T("Opowiadanie opublikowano, ale okładka nie została dodana.");
                }
                else
                {
                    StatusText = Loc.T("Publikacja nie powiodła się.");
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetForm()
    {
        StoryTitle = string.Empty;
        Description = string.Empty;
        Tags = string.Empty;
        CoverPath = string.Empty;
        Chapters.Clear();
        AddChapter();
    }

    /// <summary>Same heading rule as the in-app reader: lines starting with "# " or "## " open a chapter.</summary>
    internal static List<(string Title, string Text)> SplitIntoChapters(string text, string fallbackTitle)
    {
        var chapters = new List<(string Title, string Text)>();
        string? title = null;
        var body = new StringBuilder();

        void Flush()
        {
            var content = body.ToString().Trim();
            if (title is not null || content.Length > 0)
            {
                chapters.Add((title ?? fallbackTitle, content));
            }

            body.Clear();
        }

        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal) || line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                title = line.TrimStart('#').Trim();
            }
            else
            {
                body.AppendLine(line);
            }
        }

        Flush();
        return chapters.Count > 0 ? chapters : [(fallbackTitle, string.Empty)];
    }
}
