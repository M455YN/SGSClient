using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>"Muzyka" section: track list plus a simple play queue. The view does the actual playback.</summary>
public sealed partial class CreativeMusicViewModel(ISgsApiService? api, string apiBaseUrl)
    : CreativeSectionViewModel(api, apiBaseUrl, "music")
{
    /// <summary>Track whose comments are shown below the list, or null when the panel is closed.</summary>
    [ObservableProperty] public partial CreativeItem? CommentsItem { get; set; }

    public bool IsCommentsOpen => CommentsItem is not null;
    public string CommentsTitle => CommentsItem?.Title ?? string.Empty;

    partial void OnCommentsItemChanged(CreativeItem? value)
    {
        OnPropertyChanged(nameof(IsCommentsOpen));
        OnPropertyChanged(nameof(CommentsTitle));
    }

    /// <summary>Opens the comments of <paramref name="item"/>; a second click on the same track closes them.</summary>
    [RelayCommand]
    private void ToggleComments(CreativeItem? item)
    {
        if (item is null || CommentsItem?.Id == item.Id)
        {
            CommentsItem = null;
            Comments.Clear();
            return;
        }

        CommentsItem = item;
        _ = Comments.LoadAsync(item.Id);
    }

    protected override async Task<IEnumerable<CreativeItem>> FetchAsync(CreativeQuery query) =>
        (await Api!.GetTracksAsync(query)).Select(x => CreativeItem.FromTrack(x, ApiBaseUrl));

    /// <summary>Raised when a track must start from its beginning; the view loads <see cref="CreativeItem.FileUrl"/>.</summary>
    public event EventHandler<CreativeItem>? TrackStarted;

    [ObservableProperty] public partial CreativeItem? NowPlaying { get; set; }
    [ObservableProperty] public partial bool IsPlaying { get; set; }

    public string NowPlayingTitle => NowPlaying?.Title ?? Loc.T("Nie wybrano utworu");
    public string NowPlayingAuthor => NowPlaying?.Author ?? string.Empty;

    partial void OnNowPlayingChanged(CreativeItem? value)
    {
        OnPropertyChanged(nameof(NowPlayingTitle));
        OnPropertyChanged(nameof(NowPlayingAuthor));
    }

    [RelayCommand]
    private void Play(CreativeItem? item)
    {
        if (item is null)
        {
            return;
        }

        // Raised before NowPlaying changes so the view can swap the source before it resumes.
        TrackStarted?.Invoke(this, item);
        NowPlaying = item;
        IsPlaying = true;
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (NowPlaying is null)
        {
            Play(Items.FirstOrDefault());
            return;
        }

        IsPlaying = !IsPlaying;
    }

    /// <summary>Plays the next track in the list; stops after the last one.</summary>
    [RelayCommand]
    private void Next()
    {
        var list = Items.ToList();
        var index = NowPlaying is null ? -1 : list.FindIndex(item => item.Id == NowPlaying.Id);
        if (index + 1 < list.Count)
        {
            Play(list[index + 1]);
        }
        else
        {
            IsPlaying = false;
        }
    }
}
