using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
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

    /// <summary>First letters of the title for tiles without a cover.</summary>
    public string TileLabel => Title.Length <= 2 ? Title.ToUpperInvariant() : Title[..2].ToUpperInvariant();

    public static CreativeItem FromGame(GameDto game, string apiBaseUrl)
    {
        var build = game.Builds.FirstOrDefault(b => b.IsDefault) ?? game.Builds.FirstOrDefault();
        return new CreativeItem
        {
            Id = game.Id,
            Title = game.Title,
            Author = !string.IsNullOrWhiteSpace(game.StudioName) ? game.StudioName!
                : game.OwnerDisplayName ?? string.Empty,
            Description = game.Description ?? string.Empty,
            CoverUrl = InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, game.ThumbnailUrl),
            FileUrl = $"{apiBaseUrl.TrimEnd('/')}/api/Games/{game.Id}/file",
            FileName = build?.ExeName ?? string.Empty,
            Tags = game.Tags,
            CreatedAtUtc = game.CreatedAtUtc
        };
    }
}

/// <summary>Loads one kind of content (art, music or books) from the store catalog.</summary>
public abstract partial class CreativeSectionViewModel(ISgsApiService? api, string apiBaseUrl, GameContentKind kind)
    : ObservableObject
{
    protected string ApiBaseUrl { get; } = apiBaseUrl;
    protected ISgsApiService? Api { get; } = api;

    public ObservableCollection<CreativeItem> Items { get; } = [];

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
            var games = await Api.GetGamesAsync(new GameCatalogQuery
            {
                Kind = kind,
                Q = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                Sort = "newest"
            });
            Items.Clear();
            // An API without the "kind" filter returns every title, so keep only this section's kind.
            foreach (var game in games.Where(g => g.Kind == kind))
            {
                Items.Add(CreativeItem.FromGame(game, ApiBaseUrl));
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
