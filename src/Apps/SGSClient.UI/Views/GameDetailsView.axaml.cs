using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LibVLCSharp.Avalonia;
using LibVLCSharp.Shared;
using SGSClient.Core.Models;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class GameDetailsView : UserControl
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private VideoView? _videoView;

    public GameDetailsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is GameDetailsViewModel vm)
            {
                vm.SetOpenPropertiesHandler(OpenProperties);
            }
        };
        DetachedFromVisualTree += (_, _) => StopIntro();
    }

    private void Back_OnClick(object? sender, RoutedEventArgs e)
    {
        StopIntro();
        if (DataContext is GameDetailsViewModel vm)
        {
            vm.CloseMediaPreviewCommand.Execute(null);
            vm.CloseCommand.Execute(null);
        }
    }

    private void Cover_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == PlayOverlay || (e.Source is Control c && IsDescendantOf(c, PlayOverlay)))
        {
            return;
        }

        if (DataContext is GameDetailsViewModel { HasCover: true } vm)
        {
            vm.OpenCoverPreviewCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void MediaThumb_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm
            && (sender as Control)?.Tag is MediaThumbItem thumb)
        {
            vm.OpenMediaPreviewCommand.Execute(thumb);
            e.Handled = true;
        }
    }

    private void MediaPreviewBackdrop_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not GameDetailsViewModel vm)
        {
            return;
        }

        // Ignore clicks on buttons / image (they have their own handlers).
        if (e.Source is Button or Image)
        {
            return;
        }

        if (vm.CanPreviewNext)
        {
            vm.PreviewNextCommand.Execute(null);
        }
        else
        {
            vm.CloseMediaPreviewCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void MediaPreviewImage_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not GameDetailsViewModel vm)
        {
            return;
        }

        if (vm.CanPreviewNext)
        {
            vm.PreviewNextCommand.Execute(null);
        }
        else
        {
            vm.CloseMediaPreviewCommand.Execute(null);
        }

        e.Handled = true;
    }

    private static bool IsDescendantOf(Control? node, Control? ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = node.Parent as Control;
        }

        return false;
    }

    private async void AddToLibrary_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            await vm.AddToLibraryCommand.ExecuteAsync(null);
        }
    }

    private async void Launch_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            await vm.LaunchCommand.ExecuteAsync(null);
        }
    }

    private async void Install_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            await vm.InstallCommand.ExecuteAsync(null);
        }
    }

    private void CancelInstall_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            vm.CancelInstallCommand.Execute(null);
        }
    }

    private async void Uninstall_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            await vm.UninstallCommand.ExecuteAsync(null);
        }
    }

    private void Properties_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm)
        {
            vm.OpenPropertiesCommand.Execute(null);
        }
    }

    private void Tab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GameDetailsViewModel vm && sender is Control { Tag: string tab })
        {
            vm.SelectTabCommand.Execute(tab);
        }
    }

    private void PlayOverlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not GameDetailsViewModel { IntroMediaUrl: { Length: > 0 } url })
        {
            return;
        }

        StartIntro(url);
        e.Handled = true;
    }

    private void StartIntro(string mediaUrl)
    {
        EnsurePlayer();
        if (_videoView is null || _mediaPlayer is null || _libVlc is null)
        {
            return;
        }

        SetHeroPlaybackUi(playing: true);

        using var media = new Media(_libVlc, mediaUrl, FromType.FromLocation);
        _mediaPlayer.Play(media);
    }

    private void EnsurePlayer()
    {
        if (_videoView is not null)
        {
            return;
        }

        LibVLCSharp.Shared.Core.Initialize();
        _libVlc = new LibVLC("--no-video-title-show");
        _mediaPlayer = new MediaPlayer(_libVlc);
        _mediaPlayer.EndReached += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(StopIntro);
        };

        _videoView = new VideoView
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            MediaPlayer = _mediaPlayer
        };
        IntroHost.Children.Add(_videoView);
    }

    private void StopIntro()
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Stop();
        }

        SetHeroPlaybackUi(playing: false);
    }

    private void SetHeroPlaybackUi(bool playing)
    {
        IntroHost.IsVisible = playing;
        CoverImage.IsVisible = !playing && DataContext is GameDetailsViewModel { HasCover: true };
        TileLabelText.IsVisible = !playing && DataContext is GameDetailsViewModel { HasCover: false };
        PlayOverlay.IsVisible = !playing && DataContext is GameDetailsViewModel { HasIntro: true };
    }

    private void OpenProperties(GameLibraryItem item)
    {
        var window = new GamePropertiesWindow();
        window.ViewModel.ViewInStoreRequested += (_, _) =>
        {
            window.Close();
            if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel main)
            {
                main.OpenGameDetails(item.GameId, "Library");
            }
        };
        window.SetGame(item.GameId, item.BuildId, item.Title);
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }
}
