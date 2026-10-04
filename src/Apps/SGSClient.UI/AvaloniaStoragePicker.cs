using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace SGSClient.UI;

internal static class AvaloniaStoragePicker
{
    public static async Task<string?> PickSingleFileAsync(
        TopLevel? top,
        FilePickerFileType type,
        string? title = null)
    {
        Debug.WriteLine($"[SGS Media] PickSingle start title={title ?? "(none)"} filter={type.Name}");

        if (top is null)
        {
            Debug.WriteLine("[SGS Media] TopLevel is null — picker unavailable");
            return null;
        }

        if (!top.StorageProvider.CanOpen)
        {
            Debug.WriteLine("[SGS Media] StorageProvider.CanOpen=false");
            return null;
        }

        IReadOnlyList<IStorageFile> files;
        try
        {
            files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [type]
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SGS Media] OpenFilePickerAsync threw: {ex}");
            throw;
        }

        Debug.WriteLine($"[SGS Media] picker returned count={files.Count}");
        if (files.Count == 0)
        {
            Debug.WriteLine("[SGS Media] user cancelled or empty selection");
            return null;
        }

        return await MaterializeLocalPathAsync(files[0]);
    }

    public static async Task<IReadOnlyList<string>> PickMultipleFilesAsync(
        TopLevel? top,
        FilePickerFileType type,
        string? title = null)
    {
        Debug.WriteLine($"[SGS Media] PickMultiple start title={title ?? "(none)"} filter={type.Name}");

        if (top is null)
        {
            Debug.WriteLine("[SGS Media] TopLevel is null — picker unavailable");
            return [];
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = [type]
        });

        Debug.WriteLine($"[SGS Media] picker returned count={files.Count}");
        var paths = new List<string>(files.Count);
        foreach (var file in files)
        {
            var path = await MaterializeLocalPathAsync(file);
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static async Task<string?> MaterializeLocalPathAsync(IStorageFile file)
    {
        var local = file.TryGetLocalPath();
        Debug.WriteLine($"[SGS Media] file name={file.Name} tryGetLocalPath={local ?? "(null)"}");

        if (!string.IsNullOrWhiteSpace(local) && File.Exists(local))
        {
            return local;
        }

        var ext = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".bin";
        }

        var dir = Path.Combine(Path.GetTempPath(), "SGSClient", "picks");
        Directory.CreateDirectory(dir);
        var tempPath = Path.Combine(dir, $"{Guid.NewGuid():N}{ext}");

        try
        {
            await using var source = await file.OpenReadAsync();
            await using var destination = File.Create(tempPath);
            await source.CopyToAsync(destination);
            Debug.WriteLine($"[SGS Media] materialized brokered file → {tempPath} ({new FileInfo(tempPath).Length} bytes)");
            return tempPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SGS Media] materialize failed: {ex}");
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // ignore
            }

            throw;
        }
    }
}
