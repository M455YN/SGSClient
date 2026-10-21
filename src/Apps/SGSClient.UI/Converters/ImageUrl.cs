using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace SGSClient.UI.Converters;

/// <summary>Attached <c>ImageUrl.Source</c>: loads http(s) URLs, file:// URIs and local paths into <see cref="Image.Source"/>; null when unavailable.</summary>
public static class ImageUrl
{
    // shortcut: unbounded in-memory cache, evict if the catalog gets large.
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Cache = new();
    private static readonly HttpClient Http = new();

    public static readonly AttachedProperty<string?> SourceProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("Source", typeof(ImageUrl));

    static ImageUrl() => SourceProperty.Changed.AddClassHandler<Image>(async (image, e) =>
    {
        var url = e.NewValue as string;
        image.Source = null;
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var bitmap = await Cache.GetOrAdd(url, Load);
        if (GetSource(image) == url)
        {
            image.Source = bitmap;
        }
    });

    public static string? GetSource(Image image) => image.GetValue(SourceProperty);

    public static void SetSource(Image image, string? value) => image.SetValue(SourceProperty, value);

    private static async Task<Bitmap?> Load(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return File.Exists(url) ? new Bitmap(url) : null;
            }

            if (uri.IsFile)
            {
                return File.Exists(uri.LocalPath) ? new Bitmap(uri.LocalPath) : null;
            }

            var bytes = await Http.GetByteArrayAsync(uri);
            return new Bitmap(new MemoryStream(bytes));
        }
        catch
        {
            return null;
        }
    }
}
