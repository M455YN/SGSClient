using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;
using SGSClient.Core.Packaging;

namespace SGSClient.UI.ViewModels;

/// <summary>Manifest of a game already in the catalog, for the "download manifest" and "new version" flows.</summary>
public static class GameManifestSource
{
    /// <summary>Owners see their drafts through "mine", admins through the admin list.</summary>
    public static async Task<SgsPackageManifest?> LoadAsync(ISgsApiService api, int gameId, bool asAdmin)
    {
        var games = asAdmin ? await api.GetAdminGamesAsync() : await api.GetMyGamesAsync();
        var game = games.FirstOrDefault(g => g.Id == gameId);
        return game is null ? null : SgsPackageManifestFactory.FromGame(game);
    }

    /// <summary>Returns the status line to show; <c>null</c> when the user cancelled the save dialog.</summary>
    public static async Task<string?> SaveAsync(
        ISgsApiService api,
        int gameId,
        bool asAdmin,
        Func<string, string, Task<string?>> saveFile)
    {
        try
        {
            var manifest = await LoadAsync(api, gameId, asAdmin);
            if (manifest is null)
            {
                return Loc.T("Nie znaleziono gry — odśwież listę i spróbuj ponownie.");
            }

            var saved = await saveFile(SgsPackageManifest.FileName, SgsPackageManifestFactory.ToJson(manifest));
            return saved is null ? null : Loc.F("Zapisano manifest: {0}", saved);
        }
        catch (Exception ex)
        {
            return Loc.F("Nie udało się zapisać manifestu: {0}", ex.Message);
        }
    }
}
