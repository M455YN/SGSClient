using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;
using SGSClient.Core.Packaging;

namespace SGSClient.UI.ViewModels;

/// <summary>Manifest of a game already in the catalog, for the "download manifest" and "new version" flows.</summary>
public static class GameManifestSource
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Owners see their drafts through "mine", admins through the admin list.</summary>
    public static async Task<SgsPackageManifest?> LoadAsync(ISgsApiService api, int gameId, bool asAdmin)
    {
        var games = asAdmin ? await api.GetAdminGamesAsync() : await api.GetMyGamesAsync();
        var game = games.FirstOrDefault(g => g.Id == gameId);
        return game is null ? null : SgsPackageManifestFactory.FromGame(game);
    }

    /// <summary>
    /// Saves the manifest with the game's achievements and stats. Uploaded icons are downloaded next to it
    /// into <c>sdk_sgs/achievements/</c>, so the folder can be published as it is. Returns the status line to
    /// show; <c>null</c> when the user cancelled the save dialog.
    /// </summary>
    public static async Task<string?> SaveAsync(
        ISgsApiService api,
        int gameId,
        bool asAdmin,
        Func<string, string, Task<string?>> saveFile,
        string? apiBaseUrl = null)
    {
        try
        {
            var manifest = await LoadAsync(api, gameId, asAdmin);
            if (manifest is null)
            {
                return Loc.T("Nie znaleziono gry — odśwież listę i spróbuj ponownie.");
            }

            // Local path in the manifest → absolute URL to fetch it from.
            var downloads = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (apiBaseUrl is not null)
            {
                var definitions = await api.GetAchievementDefinitionsAsync(gameId);
                if (definitions.Count > 0)
                {
                    manifest.Achievements = definitions.Select(SgsPackageManifestFactory.ToPackageAchievement).ToList();
                    manifest.Stats = (await api.GetStatDefinitionsAsync(gameId)).ToList();
                    foreach (var definition in definitions)
                    {
                        AddDownload(definition.Key, definition.IconUrl, locked: false);
                        AddDownload(definition.Key, definition.LockedIconUrl, locked: true);
                    }
                }
            }

            var saved = await saveFile(SgsPackageManifest.FileName, SgsPackageManifestFactory.ToJson(manifest));
            if (saved is null)
            {
                return null;
            }

            if (downloads.Count == 0)
            {
                return Loc.F("Zapisano manifest: {0}", saved);
            }

            var folder = Path.IsPathRooted(saved) ? Path.GetDirectoryName(saved) : null;
            var failed = new List<string>();
            foreach (var (localPath, url) in downloads)
            {
                try
                {
                    if (folder is null)
                    {
                        throw new IOException("no local folder");
                    }

                    var target = Path.Combine(folder, localPath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await File.WriteAllBytesAsync(target, await Http.GetByteArrayAsync(url));
                }
                catch
                {
                    failed.Add(localPath);
                }
            }

            if (failed.Count > 0 && folder is not null)
            {
                // An icon that could not be copied stays a URL, so publishing never loses it.
                foreach (var achievement in manifest.Achievements!)
                {
                    if (achievement.Icon is { } icon && failed.Contains(icon))
                    {
                        achievement.Icon = downloads[icon];
                    }

                    if (achievement.LockedIcon is { } lockedIcon && failed.Contains(lockedIcon))
                    {
                        achievement.LockedIcon = downloads[lockedIcon];
                    }
                }

                await File.WriteAllTextAsync(saved, SgsPackageManifestFactory.ToJson(manifest), new System.Text.UTF8Encoding(false));
            }

            return failed.Count == 0
                ? Loc.F("Zapisano manifest z osiągnięciami ({0}); ikony są w folderze sdk_sgs/achievements obok niego.", manifest.Achievements!.Count)
                : Loc.F("Zapisano manifest: {0}. Nie udało się pobrać {1} ikon — zostały w nim jako adresy URL.", saved, failed.Count);

            void AddDownload(string key, string? iconUrl, bool locked)
            {
                if (SgsPackageManifestFactory.LocalIconPath(key, iconUrl, locked) is { } localPath
                    && InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl!, iconUrl) is { } url)
                {
                    downloads[localPath] = url;
                }
            }
        }
        catch (Exception ex)
        {
            return Loc.F("Nie udało się zapisać manifestu: {0}", ex.Message);
        }
    }
}
