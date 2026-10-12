using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace SGSClient.UI.Views;

/// <summary>Just the app logo, floating on the desktop while the client loads; closed once the shell is up.</summary>
public sealed class SplashWindow : Window
{
    public SplashWindow()
    {
        Width = 240;
        Height = 240;
        CanResize = false;
        ShowInTaskbar = false;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Title = "SGSClient";

        Content = new Image
        {
            Source = new Bitmap(AssetLoader.Open(new Uri("avares://SGSClient/Assets/app.ico"))),
            Stretch = Stretch.Uniform
        };
    }
}
