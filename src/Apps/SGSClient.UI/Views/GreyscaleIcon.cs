using SkiaSharp;

namespace SGSClient.UI.Views;

/// <summary>Makes the "unachieved" greyscale copy of an achievement icon, like Steamworks' auto-generated one.</summary>
internal static class GreyscaleIcon
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Rec. 601 luma weights; alpha is kept as is.
    private static readonly float[] Luma =
    [
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0, 0, 0, 1, 0
    ];

    /// <summary>Downloads <paramref name="url"/> and writes a greyscale PNG to a temp file; returns its path.</summary>
    public static async Task<string?> CreateAsync(string url)
    {
        var bytes = await Http.GetByteArrayAsync(url);
        using var source = SKBitmap.Decode(bytes);
        if (source is null)
        {
            return null;
        }

        using var surface = SKSurface.Create(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var paint = new SKPaint { ColorFilter = SKColorFilter.CreateColorMatrix(Luma) })
        using (var image = SKImage.FromBitmap(source))
        {
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawImage(image, 0, 0, paint);
        }

        using var snapshot = surface.Snapshot();
        using var png = snapshot.Encode(SKEncodedImageFormat.Png, 100);
        var path = Path.Combine(Path.GetTempPath(), $"sgs-locked-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, png.ToArray());
        return path;
    }
}
