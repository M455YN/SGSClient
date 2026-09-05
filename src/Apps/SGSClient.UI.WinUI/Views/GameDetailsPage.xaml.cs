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
    private Uri? _introUri;
    private bool _introPlaying;

    public GameDetailsViewModel ViewModel { get; }

    public GameDetailsPage()
    {
        ViewModel = App.Services.GetRequiredService<GameDetailsViewModel>();
        InitializeComponent();
        ViewModel.SetOpenPropertiesHandler(OpenProperties);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GameDetailsViewModel.IsMediaPreviewOpen)
                or nameof(GameDetailsViewModel.MediaPreviewUrl)
                or nameof(GameDetailsViewModel.MediaPreviewLabel)
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
            EmptyStatus.Text = "Brak identyfikatora gry.";
            ContentScroll.Visibility = Visibility.Collapsed;
            return;
        }

        await ViewModel.LoadAsync(gameId, fromLibrary, returnTag);
        RefreshUi();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        StopIntro();
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
        MorePanel.Visibility = ViewModel.ShowMoreTab ? Visibility.Visible : Visibility.Collapsed;

        RefreshMediaPreview();
    }

    private void RefreshMediaPreview()
    {
        MediaPreviewOverlay.Visibility = ViewModel.IsMediaPreviewOpen ? Visibility.Visible : Visibility.Collapsed;
        if (!ViewModel.IsMediaPreviewOpen)
        {
            MediaPreviewImage.Source = null;
            return;
        }

        MediaPreviewLabelText.Text = ViewModel.MediaPreviewLabel;
        MediaPreviewCounterText.Text = ViewModel.PreviewCounterText;
        MediaPreviewHintText.Text = ViewModel.PreviewHintText;
        PreviewPrevButton.IsEnabled = ViewModel.CanPreviewPrevious;
        PreviewNextButton.IsEnabled = ViewModel.CanPreviewNext;
        PreviewPrevButton.Opacity = ViewModel.CanPreviewPrevious ? 1 : 0.35;
        PreviewNextButton.Opacity = ViewModel.CanPreviewNext ? 1 : 0.35;

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
            CoverImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(coverUri);
            CoverImage.Visibility = Visibility.Visible;
            TileLabelText.Visibility = Visibility.Collapsed;
        }
        else
        {
            CoverImage.Source = null;
            CoverImage.Visibility = Visibility.Collapsed;
            TileLabelText.Visibility = Visibility.Visible;
        }

        // Show play button on the banner whenever intro exists (Steam-like).
        PlayOverlay.Visibility = _introUri is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PlayOverlay_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_introUri is null)
        {
            return;
        }

        StartIntroPlayback();
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

    private async void OpenProperties(GameLibraryItem item)
    {
        var dialog = new GamePropertiesDialog();
        await dialog.ShowForGameAsync(item.GameId, item.BuildId, item.Title, XamlRoot);
    }
}
