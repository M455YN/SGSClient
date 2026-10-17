using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

public enum CreativeWorksMode
{
    /// <summary>Studio: the signed-in author's own works.</summary>
    Mine,

    /// <summary>Admin: everybody's works, with the author and a search box.</summary>
    Admin
}

/// <summary>One artwork, track or story in the editor list.</summary>
public sealed class CreativeWorkRow
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? ThumbUrl { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }

    public bool HasThumb => !string.IsNullOrWhiteSpace(ThumbUrl);
    public string TileLabel => Title.Length <= 2 ? Title.ToUpperInvariant() : Title[..2].ToUpperInvariant();
    public bool ShowAuthor { get; set; }
    public string MetaText => ShowAuthor && Author.Length > 0 ? $"{Author} · {DateText}" : DateText;
    public string DateText => CreatedAtUtc == default ? string.Empty : CreatedAtUtc.LocalDateTime.ToString("d");

    private static string? Abs(string apiBaseUrl, string? relative) =>
        string.IsNullOrWhiteSpace(relative) ? null : $"{apiBaseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";

    public static CreativeWorkRow From(ArtworkDto a, string baseUrl) => new()
    {
        Id = a.Id, Title = a.Title, Author = a.AuthorDisplayName ?? string.Empty, Description = a.Description ?? string.Empty,
        Tags = a.Tags, ThumbUrl = Abs(baseUrl, a.FileUrl), CreatedAtUtc = a.CreatedAtUtc
    };

    public static CreativeWorkRow From(TrackDto t, string baseUrl) => new()
    {
        Id = t.Id, Title = t.Title, Author = t.AuthorDisplayName ?? string.Empty, Description = t.Description ?? string.Empty,
        Tags = t.Tags, ThumbUrl = Abs(baseUrl, t.CoverUrl), CreatedAtUtc = t.CreatedAtUtc
    };

    public static CreativeWorkRow From(StoryDto s, string baseUrl) => new()
    {
        Id = s.Id, Title = s.Title, Author = s.AuthorDisplayName ?? string.Empty, Description = s.Description ?? string.Empty,
        Tags = s.Tags, ThumbUrl = Abs(baseUrl, s.CoverUrl), CreatedAtUtc = s.CreatedAtUtc
    };
}

/// <summary>List + editor for the works of one section ("art" | "music" | "stories"), own (Studio) or all (Admin).</summary>
public sealed partial class CreativeWorksViewModel(ISgsApiService? api, string apiBaseUrl, string section, CreativeWorksMode mode)
    : ObservableObject
{
    private Func<string, Task<bool>>? _confirmDelete;
    private Func<Task<string?>>? _pickCover;
    private List<CreativeWorkRow> _all = [];

    public string Section { get; } = section;
    public CreativeWorksMode Mode { get; } = mode;
    public bool IsAdmin => Mode == CreativeWorksMode.Admin;
    public bool IsStories => Section == "stories";
    public bool HasCoverEditor => Section != "art";

    public ObservableCollection<CreativeWorkRow> Rows { get; } = [];
    public ObservableCollection<ChapterDraft> Chapters { get; } = [];
    public ObservableCollection<string> Issues { get; } = [];

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool IsLoaded { get; set; }
    [ObservableProperty] public partial string EditTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditTags { get; set; } = string.Empty;
    [ObservableProperty] public partial string CoverPath { get; set; } = string.Empty;
    [ObservableProperty] public partial ChapterDraft? SelectedChapter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial CreativeWorkRow? SelectedRow { get; set; }

    public bool HasSelection => SelectedRow is not null;

    public void SetConfirmDeleteHandler(Func<string, Task<bool>> confirm) => _confirmDelete = confirm;

    public void SetPickCoverHandler(Func<Task<string?>> pick) => _pickCover = pick;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedRowChanged(CreativeWorkRow? value)
    {
        Issues.Clear();
        CoverPath = string.Empty;
        Chapters.Clear();
        SelectedChapter = null;
        EditTitle = value?.Title ?? string.Empty;
        EditDescription = value?.Description ?? string.Empty;
        EditTags = value is null ? string.Empty : string.Join(", ", value.Tags);
        if (value is not null && IsStories)
        {
            _ = LoadChaptersAsync(value);
        }
    }

    // list items carry no chapters: fetch the full story before editing
    private async Task LoadChaptersAsync(CreativeWorkRow row)
    {
        if (api is null)
        {
            return;
        }

        var story = await api.GetStoryAsync(row.Id);
        if (story is null || SelectedRow != row)
        {
            return;
        }

        Chapters.Clear();
        foreach (var chapter in story.Chapters.OrderBy(c => c.Index))
        {
            Chapters.Add(new ChapterDraft { Title = chapter.Title, Text = chapter.Text });
        }

        SelectedChapter = Chapters.FirstOrDefault();
    }

    private void ApplyFilter()
    {
        var q = SearchText.Trim();
        var keepId = SelectedRow?.Id;
        Rows.Clear();
        foreach (var row in _all.Where(r => q.Length == 0
                     || r.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || r.Author.Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            Rows.Add(row);
        }

        if (SelectedRow is null || !Rows.Contains(SelectedRow))
        {
            SelectedRow = Rows.FirstOrDefault(r => r.Id == keepId) ?? Rows.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (api is null)
        {
            StatusText = Loc.T("Sklep niedostępny.");
            return;
        }

        IsBusy = true;
        try
        {
            var keepId = SelectedRow?.Id;
            var admin = IsAdmin;
            _all = Section switch
            {
                "art" => (await (admin ? api.GetAdminArtworksAsync() : api.GetMyArtworksAsync())).Select(a => CreativeWorkRow.From(a, apiBaseUrl)).ToList(),
                "music" => (await (admin ? api.GetAdminTracksAsync() : api.GetMyTracksAsync())).Select(t => CreativeWorkRow.From(t, apiBaseUrl)).ToList(),
                _ => (await (admin ? api.GetAdminStoriesAsync() : api.GetMyStoriesAsync())).Select(s => CreativeWorkRow.From(s, apiBaseUrl)).ToList()
            };
            foreach (var r in _all)
            {
                r.ShowAuthor = admin;
            }

            _all = _all.OrderByDescending(r => r.CreatedAtUtc).ToList();
            IsLoaded = true;
            SelectedRow = null;
            ApplyFilter();
            if (keepId is { } id && Rows.FirstOrDefault(r => r.Id == id) is { } same)
            {
                SelectedRow = same;
            }

            StatusText = _all.Count == 0 ? Loc.T("Nic tu jeszcze nie ma.") : string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się wczytać: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PickCoverAsync()
    {
        if (_pickCover is not null && await _pickCover() is { Length: > 0 } path)
        {
            CoverPath = path;
        }
    }

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
        if (chapter is null)
        {
            return;
        }

        var index = Chapters.IndexOf(chapter);
        Chapters.Remove(chapter);
        SelectedChapter = Chapters.Count == 0 ? null : Chapters[Math.Min(index, Chapters.Count - 1)];
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (api is null || IsBusy || SelectedRow is not { } row)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            StatusText = Loc.T("Podaj tytuł.");
            return;
        }

        var written = Chapters.Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();
        if (IsStories && written.Count == 0)
        {
            StatusText = Loc.T("Podaj tytuł i napisz przynajmniej jeden rozdział.");
            return;
        }

        IsBusy = true;
        Issues.Clear();
        StatusText = Loc.T("Zapisywanie…");
        var title = EditTitle.Trim();
        var description = string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim();
        var tags = AddMediaViewModel.ParseTags(EditTags);
        var cover = string.IsNullOrWhiteSpace(CoverPath) ? null : CoverPath;
        try
        {
            IReadOnlyList<string> errors;
            bool saved;
            switch (Section)
            {
                case "art":
                    var (art, artErrors) = await api.UpdateArtworkAsync(row.Id, new UpdateCreativeRequestDto { Title = title, Description = description, Tags = tags });
                    (errors, saved) = (artErrors, art is not null);
                    break;
                case "music":
                    var (track, trackErrors) = await api.UpdateTrackAsync(row.Id, new UpdateCreativeRequestDto { Title = title, Description = description, Tags = tags }, cover);
                    (errors, saved) = (trackErrors, track is not null);
                    break;
                default:
                    var (story, storyErrors) = await api.UpdateStoryAsync(
                        row.Id,
                        new UpdateStoryRequestDto
                        {
                            Title = title,
                            Description = description,
                            Tags = tags,
                            Chapters = written.Select(c => new CreateStoryChapterDto
                            {
                                Title = string.IsNullOrWhiteSpace(c.Title) ? Loc.T("Rozdział bez tytułu") : c.Title.Trim(),
                                Text = c.Text.Trim()
                            }).ToList()
                        },
                        cover);
                    (errors, saved) = (storyErrors, story is not null);
                    break;
            }

            if (saved)
            {
                await LoadAsync();
            }

            foreach (var error in errors)
            {
                Issues.Add(error);
            }

            StatusText = !saved
                ? Loc.T("Zapis nie powiódł się.")
                : errors.Count == 0 ? Loc.T("Zapisano zmiany.") : Loc.T("Zapisano, ale okładka nie została zmieniona.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (api is null || IsBusy || SelectedRow is not { } row)
        {
            return;
        }

        if (_confirmDelete is not null && !await _confirmDelete(row.Title))
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (await api.DeleteCreativeAsync(Section, row.Id))
            {
                var index = Rows.IndexOf(row);
                _all.Remove(row);
                Rows.Remove(row);
                SelectedRow = Rows.Count == 0 ? null : Rows[Math.Min(index, Rows.Count - 1)];
                StatusText = Loc.F("Usunięto: {0}", row.Title);
            }
            else
            {
                StatusText = Loc.T("Nie udało się usunąć.");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
