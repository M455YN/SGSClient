using SGSClient.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.Core.Models;
using SGSClient.UI.ViewModels;
using Windows.Media.Core;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class GameDetailsPage : Page
{
    private MediaPlayerElement? _introPlayer;
    private MediaPlayerElement? _previewPlayer;
    private Uri? _introUri;
    private bool _introPlaying;

    public GameDetailsViewModel ViewModel { get; }

    public GameDetailsPage()
    {
        ViewModel = App.Services.GetRequiredService<GameDetailsViewModel>();
        InitializeComponent();
        ViewModel.SetOpenPropertiesHandler(OpenProperties);
        ReviewSortBox.ItemsSource = ViewModel.ReviewSortOptions;
        ReviewFilterBox.ItemsSource = ViewModel.ReviewFilterOptions;
        ReviewSortBox.SelectedItem = ViewModel.SelectedReviewSort;
        ReviewFilterBox.SelectedItem = ViewModel.SelectedReviewFilter;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GameDetailsViewModel.IsMediaPreviewOpen)
                or nameof(GameDetailsViewModel.MediaPreviewUrl)
                or nameof(GameDetailsViewModel.MediaPreviewLabel)
                or nameof(GameDetailsViewModel.MediaPreviewIsVideo)
                or nameof(GameDetailsViewModel.PreviewCounterText)
                or nameof(GameDetailsViewModel.CanPreviewPrevious)
                or nameof(GameDetailsViewModel.CanPreviewNext)
                or nameof(GameDetailsViewModel.PreviewHintText))
            {
                RefreshMediaPreview();
                return;
            }

            RefreshUi();
        };
        Unloaded += (_, _) =>
        {
            StopIntro();
            StopPreviewVideo();
            ViewModel.CloseMediaPreviewCommand.Execute(null);
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StopIntro();

        var (gameId, fromLibrary, returnTag) = e.Parameter switch
        {
            GameDetailsNavArgs args => (args.GameId, args.ReturnTag == "Library", args.ReturnTag),
            int id => (id, false, "Games"),
            _ => (0, false, "Games")
        };

        if (gameId <= 0)
        {
            EmptyStatus.Text = Loc.T("Brak identyfikatora gry.");
            ContentScroll.Visibility = Visibility.Collapsed;
            return;
        }

        await ViewModel.LoadAsync(gameId, fromLibrary, returnTag);
        RefreshUi();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        StopIntro();
        StopPreviewVideo();
        ViewModel.CloseMediaPreviewCommand.Execute(null);
        base.OnNavigatedFrom(e);
    }

    private void RefreshUi()
    {
        EmptyStatus.Text = ViewModel.StatusText;
        EmptyStatus.Visibility = ViewModel.HasContent ? Visibility.Collapsed : Visibility.Visible;
        ContentScroll.Visibility = ViewModel.HasContent ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Content = ViewModel.BackLabel;
        ContextLabelText.Text = ViewModel.ContextLabel;
        TitleText.Text = ViewModel.Title;
        TaglineText.Text = ViewModel.Tagline;
        TileLabelText.Text = ViewModel.TileLabel;

        _introUri = ViewModel.HasIntro
            && !string.IsNullOrWhiteSpace(ViewModel.IntroMediaUrl)
            && Uri.TryCreate(ViewModel.IntroMediaUrl, UriKind.Absolute, out var introUri)
            ? introUri
            : null;

        if (_introUri is null)
        {
            StopIntro();
        }

        RefreshHeroMedia();

        StatusText.Text = ViewModel.StatusText;
        PlaytimeText.Text = ViewModel.PlaytimeText;
        PlaytimeText.Visibility = ViewModel.HasPlaytime ? Visibility.Visible : Visibility.Collapsed;
        DescriptionText.Text = ViewModel.Description;
        HardwareText.Text = ViewModel.HardwareRequirements;
        OtherText.Text = ViewModel.OtherInformation;
        PlatformsText.Text = ViewModel.PlatformsText;
        SymbolText.Text = ViewModel.Symbol;

        PrimaryActionButton.Content = ViewModel.PrimaryActionLabel;
        PrimaryActionButton.IsEnabled = ViewModel.CanAdd;
        PrimaryActionButton.Visibility = ViewModel.ShowStoreActions ? Visibility.Visible : Visibility.Collapsed;
        WishlistButton.Content = ViewModel.WishlistLabel;
        WishlistButton.Visibility = ViewModel.CanToggleWishlist ? Visibility.Visible : Visibility.Collapsed;

        LaunchButton.Visibility = ViewModel.ShowLibraryActions ? Visibility.Visible : Visibility.Collapsed;
        LaunchButton.IsEnabled = ViewModel.CanLaunch;
        LaunchButton.Content = ViewModel.LaunchLabel;
        InstallButton.Visibility = ViewModel.ShowLibraryActions ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.IsEnabled = ViewModel.CanInstall;
        UninstallButton.Visibility = ViewModel.ShowLibraryActions ? Visibility.Visible : Visibility.Collapsed;
        UninstallButton.IsEnabled = ViewModel.CanUninstall;
        PropertiesButton.Visibility = ViewModel.ShowLibraryActions ? Visibility.Visible : Visibility.Collapsed;

        InstallProgressPanel.Visibility = ViewModel.IsInstalling ? Visibility.Visible : Visibility.Collapsed;
        InstallProgressText.Text = ViewModel.InstallProgressText;
        InstallProgressBar.Value = ViewModel.InstallProgressFraction;

        BadgesList.ItemsSource = ViewModel.Badges;
        MediaThumbsList.ItemsSource = ViewModel.MediaThumbs;
        BuildsList.ItemsSource = ViewModel.Builds;

        HardwarePanel.Visibility = ViewModel.ShowHardware ? Visibility.Visible : Visibility.Collapsed;
        BuildsPanel.Visibility = ViewModel.ShowBuilds ? Visibility.Visible : Visibility.Collapsed;
        OtherPanel.Visibility = ViewModel.ShowOther ? Visibility.Visible : Visibility.Collapsed;

        DetailsPanel.Visibility = ViewModel.ShowDetailsTab ? Visibility.Visible : Visibility.Collapsed;
        ReviewsPanel.Visibility = ViewModel.ShowReviewsTab ? Visibility.Visible : Visibility.Collapsed;
        MorePanel.Visibility = ViewModel.ShowMoreTab ? Visibility.Visible : Visibility.Collapsed;
        AchievementsTabButton.Visibility = ViewModel.HasAchievementsTabButton ? Visibility.Visible : Visibility.Collapsed;
        AchievementsPanel.Visibility = ViewModel.ShowAchievementsTab ? Visibility.Visible : Visibility.Collapsed;
        AchievementsSummaryText.Text = ViewModel.AchievementsSummaryText;
        AchievementsList.ItemsSource = ViewModel.Achievements;

        ReviewsSummaryText.Text = ViewModel.ReviewsSummaryText;
        WriteReviewBox.Visibility = ViewModel.CanWriteReview ? Visibility.Visible : Visibility.Collapsed;
        CannotReviewHint.Visibility = ViewModel.CanWriteReview ? Visibility.Collapsed : Visibility.Visible;
        ReviewDraftBox.Text = ViewModel.ReviewDraftText;
        ReviewStatusText.Text = ViewModel.ReviewStatusText;
        DeleteReviewBtn.Visibility = ViewModel.HasMyReview ? Visibility.Visible : Visibility.Collapsed;
        ReviewsList.ItemsSource = ViewModel.Reviews;

        RefreshMediaPreview();
    }

    private void RefreshMediaPreview()
    {
        MediaPreviewOverlay.Visibility = ViewModel.IsMediaPreviewOpen ? Visibility.Visible : Visibility.Collapsed;
        if (!ViewModel.IsMediaPreviewOpen)
        {
            StopPreviewVideo();
            MediaPreviewImage.Source = null;
            MediaPreviewImage.Visibility = Visibility.Collapsed;
            MediaPreviewVideoHost.Visibility = Visibility.Collapsed;
            return;
        }

        MediaPreviewLabelText.Text = ViewModel.MediaPreviewLabel;
        MediaPreviewCounterText.Text = ViewModel.PreviewCounterText;
        MediaPreviewHintText.Text = ViewModel.PreviewHintText;
        PreviewPrevButton.IsEnabled = ViewModel.CanPreviewPrevious;
        PreviewNextButton.IsEnabled = ViewModel.CanPreviewNext;
        PreviewPrevButton.Opacity = ViewModel.CanPreviewPrevious ? 1 : 0.35;
        PreviewNextButton.Opacity = ViewModel.CanPreviewNext ? 1 : 0.35;

        if (ViewModel.MediaPreviewIsVideo
            && !string.IsNullOrWhiteSpace(ViewModel.MediaPreviewUrl)
            && Uri.TryCreate(ViewModel.MediaPreviewUrl, UriKind.Absolute, out var videoUri))
        {
            MediaPreviewImage.Source = null;
            MediaPreviewImage.Visibility = Visibility.Collapsed;
            MediaPreviewVideoHost.Visibility = Visibility.Visible;
            StartPreviewVideo(videoUri);
            return;
        }

        StopPreviewVideo();
        MediaPreviewVideoHost.Visibility = Visibility.Collapsed;
        MediaPreviewImage.Visibility = Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(ViewModel.MediaPreviewUrl)
            && Uri.TryCreate(ViewModel.MediaPreviewUrl, UriKind.Absolute, out var uri))
        {
            MediaPreviewImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri);
        }
        else
        {
            MediaPreviewImage.Source = null;
        }
    }

    private void StartPreviewVideo(Uri videoUri)
    {
        EnsurePreviewPlayer();
        if (_previewPlayer is null)
        {
            return;
        }

        _previewPlayer.Source = MediaSource.CreateFromUri(videoUri);
        _previewPlayer.MediaPlayer?.Play();
    }

    private void EnsurePreviewPlayer()
    {
        if (_previewPlayer is not null)
        {
            return;
        }

        _previewPlayer = new MediaPlayerElement
        {
            AreTransportControlsEnabled = true,
            AutoPlay = true,
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _previewPlayer.SetMediaPlayer(new Windows.Media.Playback.MediaPlayer());
        _previewPlayer.TransportControls.IsCompact = true;
        _previewPlayer.TransportControls.IsZoomButtonVisible = false;
        MediaPreviewVideoHost.Child = _previewPlayer;
    }

    private void StopPreviewVideo()
    {
        if (_previewPlayer?.MediaPlayer is { } player)
        {
            player.Pause();
            player.Source = null;
        }

        if (_previewPlayer is not null)
        {
            _previewPlayer.Source = null;
        }
    }

    private void Cover_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_introPlaying || IsDescendantOf(e.OriginalSource as DependencyObject, PlayOverlay))
        {
            return;
        }

        if (ViewModel.HasCover)
        {
            ViewModel.OpenCoverPreviewCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void MediaThumb_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is MediaThumbItem thumb)
        {
            ViewModel.OpenMediaPreviewCommand.Execute(thumb);
            e.Handled = true;
        }
    }

    private void CloseMediaPreview_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CloseMediaPreviewCommand.Execute(null);

    private void PreviewPrevious_Click(object sender, RoutedEventArgs e) =>
        ViewModel.PreviewPreviousCommand.Execute(null);

    private void PreviewNext_Click(object sender, RoutedEventArgs e) =>
        ViewModel.PreviewNextCommand.Execute(null);

    private void MediaPreviewBackdrop_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Clicking dimmed backdrop advances to next (or closes when on last).
        if (!ReferenceEquals(e.OriginalSource, MediaPreviewOverlay))
        {
            return;
        }

        if (ViewModel.CanPreviewNext)
        {
            ViewModel.PreviewNextCommand.Execute(null);
        }
        else
        {
            ViewModel.CloseMediaPreviewCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void MediaPreviewImage_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (ViewModel.MediaPreviewIsVideo)
        {
            return;
        }

        if (ViewModel.CanPreviewNext)
        {
            ViewModel.PreviewNextCommand.Execute(null);
        }
        else
        {
            ViewModel.CloseMediaPreviewCommand.Execute(null);
        }

        e.Handled = true;
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject? ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
        }

        return false;
    }

    private Uri? _shownCoverUri;

    private void RefreshHeroMedia()
    {
        if (_introPlaying)
        {
            CoverImage.Visibility = Visibility.Collapsed;
            TileLabelText.Visibility = Visibility.Collapsed;
            PlayOverlay.Visibility = Visibility.Collapsed;
            IntroHost.Visibility = Visibility.Visible;
            return;
        }

        IntroHost.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrWhiteSpace(ViewModel.CoverImageUrl)
            && Uri.TryCreate(ViewModel.CoverImageUrl, UriKind.Absolute, out var coverUri))
        {
            // Reload only when the URL changed, otherwise every progress tick flickers the header.
            if (_shownCoverUri != coverUri)
            {
                CoverImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(coverUri);
                _shownCoverUri = coverUri;
            }

            CoverImage.Visibility = Visibility.Visible;
            TileLabelText.Visibility = Visibility.Collapsed;
        }
        else
        {
            CoverImage.Source = null;
            _shownCoverUri = null;
            CoverImage.Visibility = Visibility.Collapsed;
            TileLabelText.Visibility = Visibility.Visible;
        }

        // Show play button on the banner whenever intro exists (Steam-like).
        PlayOverlay.Visibility = _introUri is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PlayOverlay_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!ViewModel.HasIntro)
        {
            return;
        }

        StopIntro();
        ViewModel.OpenIntroPreviewCommand.Execute(null);
        e.Handled = true;
    }

    private void StartIntroPlayback()
    {
        if (_introUri is null)
        {
            return;
        }

        EnsureIntroPlayer();
        _introPlaying = true;
        RefreshHeroMedia();

        _introPlayer!.Source = MediaSource.CreateFromUri(_introUri);
        _introPlayer.MediaPlayer?.Play();
    }

    private void EnsureIntroPlayer()
    {
        if (_introPlayer is not null)
        {
            return;
        }

        _introPlayer = new MediaPlayerElement
        {
            AreTransportControlsEnabled = true,
            AutoPlay = true,
            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        _introPlayer.SetMediaPlayer(new Windows.Media.Playback.MediaPlayer());
        _introPlayer.TransportControls.IsCompact = true;
        _introPlayer.TransportControls.IsZoomButtonVisible = false;
        _introPlayer.MediaPlayer!.MediaEnded += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(StopIntro);
        };
        IntroHost.Child = _introPlayer;
    }

    private void StopIntro()
    {
        if (_introPlayer?.MediaPlayer is { } player)
        {
            player.Pause();
            player.Source = null;
        }

        if (_introPlayer is not null)
        {
            _introPlayer.Source = null;
        }

        _introPlaying = false;
        if (IntroHost is not null)
        {
            RefreshHeroMedia();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        StopIntro();
        var returnTag = ViewModel.ReturnTag;
        if (returnTag == "Library")
        {
            App.MainWindow?.NavigateTo(typeof(LibraryPage), selectTag: "Library");
            return;
        }

        if (returnTag == "Wishlist")
        {
            App.MainWindow?.NavigateTo(typeof(WishlistPage), selectTag: "Wishlist");
            return;
        }

        if (Frame.CanGoBack)
        {
            Frame.GoBack();
            return;
        }

        App.MainWindow?.NavigateTo(typeof(GamesPage), selectTag: "Games");
    }

    private async void AddToLibrary_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddToLibraryCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void Wishlist_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ToggleWishlistCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LaunchCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.InstallCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void CancelInstall_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelInstallCommand.Execute(null);
        RefreshUi();
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.UninstallCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void Properties_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenPropertiesCommand.Execute(null);
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string tab)
        {
            ViewModel.SelectTabCommand.Execute(tab);
            RefreshUi();
        }
    }

    private void RecommendYes_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ReviewRecommended = true;
        RefreshUi();
    }

    private void RecommendNo_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ReviewRecommended = false;
        RefreshUi();
    }

    private async void SubmitReview_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ReviewDraftText = ReviewDraftBox.Text ?? string.Empty;
        await ViewModel.SubmitReviewCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void DeleteReview_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DeleteReviewCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void ReviewFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ReviewSortBox.SelectedItem is string sort)
        {
            ViewModel.SelectedReviewSort = sort;
        }

        if (ReviewFilterBox.SelectedItem is string filter)
        {
            ViewModel.SelectedReviewFilter = filter;
        }
    }

    private async void VoteHelpful_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameReviewItem item })
        {
            await ViewModel.VoteHelpfulCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void ToggleComments_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameReviewItem item })
        {
            await ViewModel.ToggleCommentsCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void AddComment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameReviewItem item })
        {
            await ViewModel.AddCommentCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void DeleteComment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: GameReviewCommentItem comment })
        {
            await ViewModel.DeleteCommentCommand.ExecuteAsync(comment);
            RefreshUi();
        }
    }

    private async void OpenProperties(GameLibraryItem item)
    {
        var dialog = new GamePropertiesDialog();
        await dialog.ShowForGameAsync(item.GameId, item.BuildId, item.Title, XamlRoot);
    }
}
