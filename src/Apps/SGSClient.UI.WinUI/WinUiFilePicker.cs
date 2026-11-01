using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

namespace SGSClient.UI.WinUI;

/// <summary>
/// Windows App SDK 1.8+ pickers. Legacy <c>Windows.Storage.Pickers</c> often fail silently in packaged WinUI apps.
/// </summary>
internal static class WinUiFilePicker
{
    public static async Task<string?> PickSingleFileAsync(
        Window window,
        IEnumerable<string> extensions,
        string? commitLabel = null,
        PickerViewMode viewMode = PickerViewMode.Thumbnail,
        PickerLocationId startLocation = PickerLocationId.PicturesLibrary)
    {
        var picker = new FileOpenPicker(window.AppWindow.Id)
        {
            ViewMode = viewMode,
            SuggestedStartLocation = startLocation
        };
        if (!string.IsNullOrWhiteSpace(commitLabel))
        {
            picker.CommitButtonText = commitLabel;
        }

        foreach (var ext in extensions)
        {
            picker.FileTypeFilter.Add(ext);
        }

        var result = await picker.PickSingleFileAsync();
        return string.IsNullOrWhiteSpace(result?.Path) ? null : result.Path;
    }

    public static async Task<IReadOnlyList<string>> PickMultipleFilesAsync(
        Window window,
        IEnumerable<string> extensions,
        string? commitLabel = null,
        PickerViewMode viewMode = PickerViewMode.Thumbnail,
        PickerLocationId startLocation = PickerLocationId.PicturesLibrary)
    {
        var picker = new FileOpenPicker(window.AppWindow.Id)
        {
            ViewMode = viewMode,
            SuggestedStartLocation = startLocation
        };
        if (!string.IsNullOrWhiteSpace(commitLabel))
        {
            picker.CommitButtonText = commitLabel;
        }

        foreach (var ext in extensions)
        {
            picker.FileTypeFilter.Add(ext);
        }

        var results = await picker.PickMultipleFilesAsync();
        if (results is null || results.Count == 0)
        {
            return [];
        }

        return results
            .Select(r => r.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
    }

    /// <summary>Returns the saved file's path; null when cancelled.</summary>
    public static async Task<string?> SaveTextFileAsync(Window window, string suggestedName, string content)
    {
        var extension = Path.GetExtension(suggestedName);
        var picker = new FileSavePicker(window.AppWindow.Id)
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName),
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeChoices.Add(extension.TrimStart('.').ToUpperInvariant(), [extension]);

        var result = await picker.PickSaveFileAsync();
        if (string.IsNullOrWhiteSpace(result?.Path))
        {
            return null;
        }

        await File.WriteAllTextAsync(result.Path, content, new System.Text.UTF8Encoding(false));
        return result.Path;
    }

    public static async Task<string?> PickFolderAsync(
        Window window,
        string? commitLabel = null,
        PickerLocationId startLocation = PickerLocationId.ComputerFolder)
    {
        var picker = new FolderPicker(window.AppWindow.Id)
        {
            SuggestedStartLocation = startLocation,
            ViewMode = PickerViewMode.List
        };
        if (!string.IsNullOrWhiteSpace(commitLabel))
        {
            picker.CommitButtonText = commitLabel;
        }

        var result = await picker.PickSingleFolderAsync();
        return string.IsNullOrWhiteSpace(result?.Path) ? null : result.Path;
    }
}
