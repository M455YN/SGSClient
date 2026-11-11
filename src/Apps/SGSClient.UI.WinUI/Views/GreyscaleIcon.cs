using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SGSClient.UI.WinUI.Views;

/// <summary>Makes the "unachieved" greyscale copy of an achievement icon, like Steamworks' auto-generated one.</summary>
internal static class GreyscaleIcon
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Downloads <paramref name="url"/> and writes a greyscale PNG to a temp file; returns its path.</summary>
    public static async Task<string?> CreateAsync(string url)
    {
        var bytes = await Http.GetByteArrayAsync(url);

        using var input = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(input))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        input.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(input);
        var pixels = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            new BitmapTransform(),
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);
        var data = pixels.DetachPixelData();
        for (var i = 0; i + 3 < data.Length; i += 4)
        {
            var luma = (byte)Math.Round((0.114 * data[i]) + (0.587 * data[i + 1]) + (0.299 * data[i + 2]));
            data[i] = data[i + 1] = data[i + 2] = luma;
        }

        var path = Path.Combine(Path.GetTempPath(), $"sgs-locked-{Guid.NewGuid():N}.png");
        await using (var file = File.Create(path))
        {
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, file.AsRandomAccessStream());
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                decoder.OrientedPixelWidth,
                decoder.OrientedPixelHeight,
                96,
                96,
                data);
            await encoder.FlushAsync();
        }

        return path;
    }
}
