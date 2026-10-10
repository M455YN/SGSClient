using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace SGSClient.UI.Converters;

/// <summary>Turns a local file path or file:// URI into a <see cref="Bitmap"/>; null when missing or unreadable.</summary>
public sealed class FileUriToBitmapConverter : IValueConverter
{
    public static FileUriToBitmapConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var path = Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.IsFile
                ? uri.LocalPath
                : text;
            return File.Exists(path) ? new Bitmap(path) : null;
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
