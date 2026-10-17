using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class CreativeMusicSection : UserControl
{
    private readonly MediaPlayer _player = new();
    private CreativeMusicViewModel? _viewModel;
    private TimeSpan _duration;
    private bool _updatingSeek;

    public CreativeMusicSection()
    {
        InitializeComponent();

        _player.PlaybackSession.PositionChanged += (session, _) =>
        {
            var position = session.Position;
            DispatcherQueue.TryEnqueue(() => ShowPosition(position));
        };
        _player.PlaybackSession.NaturalDurationChanged += (session, _) =>
        {
            var duration = session.NaturalDuration;
            DispatcherQueue.TryEnqueue(() =>
            {
                _duration = duration;
                SetSeekRange(Math.Max(1, duration.TotalSeconds));
                UpdateTimeText(TimeSpan.FromSeconds(SeekSlider.Value));
            });
        };
        _player.MediaEnded += (_, _) => DispatcherQueue.TryEnqueue(() => _viewModel?.NextCommand.Execute(null));
        _player.MediaFailed += (_, args) => DispatcherQueue.TryEnqueue(() =>
        {
            if (_viewModel is not null)
            {
                _viewModel.StatusText = Loc.F("Nie udało się odtworzyć: {0}", args.ErrorMessage);
            }
        });
        Unloaded += (_, _) =>
        {
            _player.Pause();
            if (_viewModel is not null)
            {
                _viewModel.IsPlaying = false;
            }
        };
    }

    /// <summary>Assigned by <see cref="CreativePage"/> right after construction.</summary>
    public CreativeMusicViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            if (value is not null)
            {
                value.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(CreativeMusicViewModel.IsPlaying))
                    {
                        SyncPlayback();
                    }

                    RefreshUi();
                };
                value.Items.CollectionChanged += (_, _) => RefreshUi();
                value.TrackStarted += (_, item) => StartTrack(item);
            }

            RefreshUi();
        }
    }

    private void RefreshUi()
    {
        if (_viewModel is null)
        {
            return;
        }

        StatusText.Text = _viewModel.StatusText;
        NowPlayingTitle.Text = _viewModel.NowPlayingTitle;
        NowPlayingAuthor.Text = _viewModel.NowPlayingAuthor;
        PlayPauseIcon.Glyph = _viewModel.IsPlaying ? "" : "";
        ItemsList.ItemsSource = _viewModel.Items.ToList();
    }

    private void StartTrack(CreativeItem item)
    {
        _player.Source = MediaSource.CreateFromUri(new Uri(item.FileUrl));
        _duration = TimeSpan.Zero;
        SetSeekRange(1);
        ShowPosition(TimeSpan.Zero);
    }

    /// <summary>Changing the range can clamp the thumb; keep that from being sent back as a seek.</summary>
    private void SetSeekRange(double maximum)
    {
        _updatingSeek = true;
        SeekSlider.Maximum = maximum;
        _updatingSeek = false;
    }

    private void SyncPlayback()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.IsPlaying)
        {
            _player.Play();
        }
        else
        {
            _player.Pause();
        }
    }

    private void ShowPosition(TimeSpan position)
    {
        _updatingSeek = true;
        SeekSlider.Value = position.TotalSeconds;
        _updatingSeek = false;
        UpdateTimeText(position);
    }

    private void UpdateTimeText(TimeSpan position)
    {
        TimeText.Text = $"{FormatTime(position)} / {FormatTime(_duration)}";
    }

    private static string FormatTime(TimeSpan time) => time.ToString(@"m\:ss");

    private void Cover_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image { DataContext: CreativeItem { HasCover: true } item } image)
        {
            image.Source = new BitmapImage(new Uri(item.CoverUrl!));
        }
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter || _viewModel is null)
        {
            return;
        }

        _viewModel.SearchText = SearchBox.Text.Trim();
        _ = _viewModel.LoadAsync();
        e.Handled = true;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CreativeItem item })
        {
            _viewModel?.PlayCommand.Execute(item);
        }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e) => _viewModel?.TogglePlayCommand.Execute(null);

    private void Next_Click(object sender, RoutedEventArgs e) => _viewModel?.NextCommand.Execute(null);

    private void Seek_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingSeek)
        {
            _player.PlaybackSession.Position = TimeSpan.FromSeconds(e.NewValue);
        }
    }

    private void Volume_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        _player.Volume = e.NewValue / 100d;
    }
}
