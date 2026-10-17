using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>A published book, track or artwork as the "Twórczość" hub shows it.</summary>
public sealed class CreativeItem
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string Author { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    /// <summary>Absolute cover/thumbnail URL, or null.</summary>
    public string? CoverUrl { get; init; }

    /// <summary>Absolute URL that streams the main file (image, audio or text) with range support.</summary>
    public required string FileUrl { get; init; }

    /// <summary>Main file name inside the package, e.g. "song.mp3"; its extension tells the viewer what to do.</summary>
    public string FileName { get; init; } = string.Empty;

    public string Extension => Path.GetExtension(FileName).ToLowerInvariant();
    public IReadOnlyList<string> Tags { get; init; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; }
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverUrl);
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Author);
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasTags => Tags.Count > 0;
    public string TagsText => string.Join(", ", Tags);
    public string PublishedText => CreatedAtUtc == default ? string.Empty : CreatedAtUtc.LocalDateTime.ToString("d MMMM yyyy");
    public string AuthorInitial => HasAuthor ? Author[..1].ToUpperInvariant() : "?";

    /// <summary>First letters of the title for tiles without a cover.</summary>
    public string TileLabel => Title.Length <= 2 ? Title.ToUpperInvariant() : Title[..2].ToUpperInvariant();

    private static string Abs(string apiBaseUrl, string? relative) =>
        string.IsNullOrWhiteSpace(relative) ? string.Empty : $"{apiBaseUrl.TrimEnd('/')}/{relative.TrimStart('/')}";

    public static CreativeItem FromArtwork(ArtworkDto art, string apiBaseUrl)
    {
        var url = Abs(apiBaseUrl, art.FileUrl);
        return new CreativeItem
        {
            Id = art.Id,
            Title = art.Title,
            Author = art.AuthorDisplayName ?? string.Empty,
            Description = art.Description ?? string.Empty,
            CoverUrl = url,
            FileUrl = url,
            FileName = art.FileName,
            Tags = art.Tags,
            CreatedAtUtc = art.CreatedAtUtc
        };
    }

    public static CreativeItem FromTrack(TrackDto track, string apiBaseUrl) => new()
    {
        Id = track.Id,
        Title = track.Title,
        Author = track.AuthorDisplayName ?? string.Empty,
        Description = track.Description ?? string.Empty,
        CoverUrl = string.IsNullOrWhiteSpace(track.CoverUrl) ? null : Abs(apiBaseUrl, track.CoverUrl),
        FileUrl = Abs(apiBaseUrl, track.FileUrl),
        FileName = track.FileName,
        Tags = track.Tags,
        CreatedAtUtc = track.CreatedAtUtc
    };

    /// <summary>Stories have no single file: <see cref="FileUrl"/> is empty and the text comes from the story endpoint.</summary>
    public static CreativeItem FromStory(StoryDto story, string apiBaseUrl) => new()
    {
        Id = story.Id,
        Title = story.Title,
        Author = story.AuthorDisplayName ?? string.Empty,
        Description = story.Description ?? string.Empty,
        CoverUrl = string.IsNullOrWhiteSpace(story.CoverUrl) ? null : Abs(apiBaseUrl, story.CoverUrl),
        FileUrl = string.Empty,
        Tags = story.Tags,
        CreatedAtUtc = story.CreatedAtUtc
    };
}

/// <summary>Loads one kind of content (art, music or stories) from its own endpoint.</summary>
public abstract partial class CreativeSectionViewModel(ISgsApiService? api, string apiBaseUrl)
    : ObservableObject
{
    protected string ApiBaseUrl { get; } = apiBaseUrl;
    protected ISgsApiService? Api { get; } = api;

    public ObservableCollection<CreativeItem> Items { get; } = [];

    /// <summary>Fetches this section's items from its endpoint, already mapped to <see cref="CreativeItem"/>.</summary>
    protected abstract Task<IEnumerable<CreativeItem>> FetchAsync(CreativeQuery query);

    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial CreativeItem? SelectedItem { get; set; }
    [ObservableProperty] public partial bool HasItems { get; set; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (Api is null)
        {
            StatusText = Loc.T("Sklep niedostępny.");
            return;
        }

        IsBusy = true;
        try
        {
            var items = await FetchAsync(new CreativeQuery
            {
                Q = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                Sort = "newest"
            });
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            HasItems = Items.Count > 0;
            StatusText = HasItems ? string.Empty : Loc.T("Nic tu jeszcze nie ma.");
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
}

/// <summary>Hub with three sections: Grafika (DeviantArt-like), Muzyka (SoundCloud-like), Opowiadania (Wattpad-like).</summary>
public sealed partial class CreativeViewModel : ObservableObject
{
    public CreativeViewModel()
        : this(new CreativeArtViewModel(null, string.Empty), new CreativeMusicViewModel(null, string.Empty),
            new CreativeStoriesViewModel(null, string.Empty))
    {
    }

    public CreativeViewModel(CreativeArtViewModel art, CreativeMusicViewModel music, CreativeStoriesViewModel stories)
    {
        Art = art;
        Music = music;
        Stories = stories;
    }

    public CreativeArtViewModel Art { get; }
    public CreativeMusicViewModel Music { get; }
    public CreativeStoriesViewModel Stories { get; }

    /// <summary>Art | Music | Stories</summary>
    [ObservableProperty] public partial string SelectedSection { get; set; } = "Art";

    public bool ShowArt => SelectedSection == "Art";
    public bool ShowMusic => SelectedSection == "Music";
    public bool ShowStories => SelectedSection == "Stories";

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(ShowArt));
        OnPropertyChanged(nameof(ShowMusic));
        OnPropertyChanged(nameof(ShowStories));
        _ = CurrentSection.LoadAsync();
    }

    private CreativeSectionViewModel CurrentSection => SelectedSection switch
    {
        "Music" => Music,
        "Stories" => Stories,
        _ => Art
    };

    [RelayCommand]
    private void SelectSection(string? section)
    {
        if (section is "Art" or "Music" or "Stories")
        {
            SelectedSection = section;
        }
    }

    public Task LoadAsync() => CurrentSection.LoadAsync();
}
