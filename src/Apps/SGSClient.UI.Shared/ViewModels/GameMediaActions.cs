using SGSClient.Core.Localization;
using System.Diagnostics;
using SGSClient.Core.DTOs;

namespace SGSClient.UI.ViewModels;

public static class GameMediaActions
{
    public static async Task PickAsync(
        Func<Task<string?>>? pick,
        int? gameId,
        Func<int, string, Task<GameDto?>> upload,
        Func<GameDto?, string, Task> reload,
        string okText)
    {
        Debug.WriteLine($"[SGS Media] PickAsync begin gameId={gameId} pickHandler={(pick is null ? "NULL" : "ok")} okText={okText}");

        if (pick is null)
        {
            Debug.WriteLine("[SGS Media] pick handler is not wired");
            await reload(null, Loc.T("Błąd: picker nie jest podpięty (handler null)."));
            return;
        }

        if (gameId is null)
        {
            Debug.WriteLine("[SGS Media] no selected game");
            await reload(null, Loc.T("Błąd: nie wybrano gry."));
            return;
        }

        string? path;
        try
        {
            path = await pick();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SGS Media] picker exception: {ex}");
            await reload(null, Loc.F("Błąd pickera: {0}", ex.Message));
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            Debug.WriteLine("[SGS Media] pick returned empty path (cancel or unresolved storage path)");
            return;
        }

        Debug.WriteLine($"[SGS Media] uploading gameId={gameId.Value} path={path} exists={File.Exists(path)} size={(File.Exists(path) ? new FileInfo(path).Length : -1)}");

        try
        {
            var updated = await upload(gameId.Value, path);
            Debug.WriteLine($"[SGS Media] upload result={(updated is null ? "NULL" : $"ok id={updated.Id} logo={updated.LogoUrl} cover={updated.CoverUrl} shots={updated.Screenshots?.Count}")}");
            await reload(updated, okText);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SGS Media] upload exception: {ex}");
            await reload(null, Loc.F("Błąd uploadu: {0}", ex.Message));
        }
    }

    public static async Task ClearAsync(
        int? gameId,
        Func<int, Task<GameDto?>> clear,
        Func<GameDto?, string, Task> reload,
        string okText)
    {
        Debug.WriteLine($"[SGS Media] ClearAsync gameId={gameId} okText={okText}");
        if (gameId is null)
        {
            return;
        }

        var updated = await clear(gameId.Value);
        Debug.WriteLine($"[SGS Media] clear result={(updated is null ? "NULL" : "ok")}");
        await reload(updated, okText);
    }
}
