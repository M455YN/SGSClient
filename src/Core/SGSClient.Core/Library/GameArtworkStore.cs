using SGSClient.Core.Localization;

namespace SGSClient.Core.Library;

/// <summary>Local custom capsule/tile art for library games (Steam-like).</summary>
public static class GameArtworkStore
{
    public static string ArtworkDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SGSClient",
        "artwork");

    public static async Task<string> SaveCustomCoverAsync(
        int gameId,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException(Loc.T("Nie znaleziono pliku obrazka."), sourcePath);
        }

        Directory.CreateDirectory(ArtworkDirectory);

        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 8)
        {
            ext = ".png";
        }

        ext = ext.ToLowerInvariant();
        var destination = Path.Combine(ArtworkDirectory, $"{gameId}{ext}");

        foreach (var existing in Directory.EnumerateFiles(ArtworkDirectory, $"{gameId}.*"))
        {
            if (!string.Equals(existing, destination, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(existing);
                }
                catch
                {
                    // Best-effort cleanup of previous custom covers.
                }
            }
        }

        await using (var source = File.OpenRead(sourcePath))
        await using (var target = File.Create(destination))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        return destination;
    }

    public static void ClearCustomCover(int gameId, string? knownPath = null)
    {
        if (!string.IsNullOrWhiteSpace(knownPath) && File.Exists(knownPath))
        {
            try
            {
                File.Delete(knownPath);
            }
            catch
            {
                // ignore
            }
        }

        if (!Directory.Exists(ArtworkDirectory))
        {
            return;
        }

        foreach (var existing in Directory.EnumerateFiles(ArtworkDirectory, $"{gameId}.*"))
        {
            try
            {
                File.Delete(existing);
            }
            catch
            {
                // ignore
            }
        }
    }
}
