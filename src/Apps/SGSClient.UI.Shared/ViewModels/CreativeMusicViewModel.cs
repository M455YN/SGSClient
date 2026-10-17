using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>"Muzyka" section: track list plus a simple play queue. The view does the actual playback.</summary>
public sealed partial class CreativeMusicViewModel(ISgsApiService? api, string apiBaseUrl)
    : CreativeSectionViewModel(api, apiBaseUrl, GameContentKind.Music)
{
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
