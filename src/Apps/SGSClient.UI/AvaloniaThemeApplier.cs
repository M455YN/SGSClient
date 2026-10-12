using Avalonia;
using Avalonia.Styling;

namespace SGSClient.UI;

public sealed class AvaloniaThemeApplier : IThemeApplier
{
    public static ThemeVariant ToVariant(string? theme) => theme switch
    {
        "light" => ThemeVariant.Light,
        "dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };

    public void Apply(string theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = ToVariant(theme);
        }
    }
}
