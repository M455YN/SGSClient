using Microsoft.UI.Xaml;

namespace SGSClient.UI.WinUI;

public sealed class WinUiThemeApplier : IThemeApplier
{
    public void Apply(string theme)
    {
        if (App.MainWindow?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "light" => ElementTheme.Light,
                "dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }
}
