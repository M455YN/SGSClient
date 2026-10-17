using SGSClient.Core.Localization;
using System.Net.Http.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Services;

/// <summary>
/// Catalog / library / admin / review API client. The bearer token is added by
/// <see cref="ApiBearerHandler"/>, so methods here only describe the route and payload.
/// </summary>
public sealed class HttpSgsApiService(
    HttpClient http,
    ICatalogCacheStore? catalogCache = null) : ISgsApiService
{
    // ---- request helpers -------------------------------------------------------------------

    /// <summary>Sends a request and returns the JSON body; null on a non-success status or any failure.</summary>
    private async Task<T?> SendJsonAsync<T>(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var request = CreateRequest(method, url, body);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Like <see cref="SendJsonAsync{T}"/> for list endpoints; empty list on failure.</summary>
    private async Task<IReadOnlyList<T>> SendListAsync<T>(
        string url,
        CancellationToken cancellationToken) =>
        await SendJsonAsync<List<T>>(HttpMethod.Get, url, body: null, cancellationToken).ConfigureAwait(false) ?? [];

    /// <summary>Sends a request and reports only whether the server accepted it.</summary>
    private async Task<bool> SendAsync(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateRequest(method, url, body);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Sends a request and returns either the parsed body or the server error text.</summary>
    private async Task<(T? Value, string? Error)> SendWithErrorAsync<T>(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var request = CreateRequest(method, url, body);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            return (value, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body) =>
        new(method, url) { Content = body is null ? null : JsonContent.Create(body) };

    // ---- catalog ---------------------------------------------------------------------------

    public async Task<IReadOnlyList<GameDto>> GetGamesAsync(
        GameCatalogQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildGamesUrl(query);
            var games = await http.GetFromJsonAsync<List<GameDto>>(url, cancellationToken)
                .ConfigureAwait(false) ?? [];
            if (catalogCache is not null && IsDefaultCatalogQuery(query))
            {
                try
                {
                    await catalogCache.SaveCatalogAsync(games, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // cache write is best-effort
                }
            }

            return games;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    public Task<GameDto?> GetGameAsync(int id, CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameDto>(HttpMethod.Get, $"api/Games/{id}", null, cancellationToken);

    public Task<IReadOnlyList<GameDlcDto>> GetGameDlcsAsync(
        int gameId,
        CancellationToken cancellationToken = default) =>
        SendListAsync<GameDlcDto>($"api/Games/{gameId}/dlcs", cancellationToken);

    // ---- library / wishlist ----------------------------------------------------------------

    public Task<IReadOnlyList<GameDto>> GetMyGamesAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<GameDto>("api/me/games", cancellationToken);

    public Task<IReadOnlyList<GameDto>> GetMyLibraryAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<GameDto>("api/me/library", cancellationToken);

    public Task<GameDto?> ClaimGameAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameDto>(HttpMethod.Post, $"api/me/library/{gameId}", null, cancellationToken);

    public Task<bool> RemoveFromLibraryAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/me/library/{gameId}", null, cancellationToken);

    public async Task<IReadOnlyList<GameDto>> GetWishlistAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/me/wishlist");
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                {
                    throw new HttpRequestException($"Wishlist HTTP {(int)response.StatusCode}");
                }

                return [];
            }

            var games = await response.Content.ReadFromJsonAsync<List<GameDto>>(cancellationToken)
                .ConfigureAwait(false) ?? [];
            if (catalogCache is not null)
            {
                try
                {
                    await catalogCache.SaveWishlistAsync(games, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // best-effort
                }
            }

            return games;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    public Task<GameDto?> AddToWishlistAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameDto>(HttpMethod.Post, $"api/me/wishlist/{gameId}", null, cancellationToken);

    public Task<bool> RemoveFromWishlistAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/me/wishlist/{gameId}", null, cancellationToken);

    public Task<bool> SendPresenceAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/me/presence", null, cancellationToken);

    // ---- owner / admin game management -----------------------------------------------------

    public Task<GameDto?> UpdateGameAsync(
        int gameId,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameDto>(HttpMethod.Patch, $"api/Games/{gameId}", request, cancellationToken);

    private static string BuildGamesUrl(GameCatalogQuery? query)
    {
        if (query is null)
        {
            return "api/Games";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            parts.Add($"q={Uri.EscapeDataString(query.Q.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            parts.Add($"genre={Uri.EscapeDataString(query.Genre.Trim())}");
        }

        foreach (var genre in query.Genres ?? [])
        {
            parts.Add($"genres={Uri.EscapeDataString(genre.Trim())}");
        }

        foreach (var engine in query.Engines ?? [])
        {
            parts.Add($"engines={Uri.EscapeDataString(engine.Trim())}");
        }

        if (query.Platform is { } platform)
        {
            parts.Add($"platform={(int)platform}");
        }

        if (query.Kind is { } kind)
        {
            parts.Add($"kind={(int)kind}");
        }

        if (query.Features is { } features && features != GameFeatures.None)
        {
            parts.Add($"features={(int)features}");
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            parts.Add($"tag={Uri.EscapeDataString(query.Tag.Trim())}");
        }

        if (query.ReleaseStatus is { } status)
        {
            parts.Add($"releaseStatus={(int)status}");
        }

        if (query.Featured is { } featured)
        {
            parts.Add($"featured={featured.ToString().ToLowerInvariant()}");
        }

        if (!string.IsNullOrWhiteSpace(query.Sort))
        {
            parts.Add($"sort={Uri.EscapeDataString(query.Sort.Trim())}");
        }

        return parts.Count == 0 ? "api/Games" : "api/Games?" + string.Join('&', parts);
    }

    private static bool IsDefaultCatalogQuery(GameCatalogQuery? query)
    {
        if (query is null)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(query.Q)
            && string.IsNullOrWhiteSpace(query.Genre)
            && string.IsNullOrWhiteSpace(query.Tag)
            && (query.Genres is null || query.Genres.Count == 0)
            && (query.Engines is null || query.Engines.Count == 0)
            && query.Platform is null
            && query.Kind is null
            && (query.Features is null || query.Features == GameFeatures.None)
            && query.ReleaseStatus is null
            && query.Featured is null
            && (string.IsNullOrWhiteSpace(query.Sort) || query.Sort is "featured" or "title" or "newest");
    }

    public Task<bool> DeleteGameAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/Games/{gameId}", null, cancellationToken);

    public Task<IReadOnlyList<GameDto>> GetAdminGamesAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<GameDto>("api/admin/games", cancellationToken);

    public Task<GameDto?> UpdateAdminGameAsync(
        int gameId,
        UpdateGameRequestDto request,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameDto>(HttpMethod.Patch, $"api/admin/games/{gameId}", request, cancellationToken);

    public Task<bool> DeleteAdminGameAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/admin/games/{gameId}", null, cancellationToken);

    public Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<AdminUserDto>("api/admin/users", cancellationToken);

    public async Task<AdminRoleUpdateResult> UpdateAdminUserRoleAsync(
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Patch, $"api/admin/users/{userId}/role")
            {
                Content = JsonContent.Create(new UpdateUserRoleRequestDto { Role = role })
            };
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new AdminRoleUpdateResult
                {
                    Error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false)
                };
            }

            var updated = await response.Content.ReadFromJsonAsync<AdminUserDto>(cancellationToken)
                .ConfigureAwait(false);
            return updated is null
                ? new AdminRoleUpdateResult { Error = Loc.T("Serwer nie zwrócił zaktualizowanego użytkownika.") }
                : new AdminRoleUpdateResult { User = updated };
        }
        catch (Exception ex)
        {
            return new AdminRoleUpdateResult { Error = ex.Message };
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiErrorPayload>(cancellationToken)
                .ConfigureAwait(false);
            if (payload?.Errors is { Length: > 0 } errors)
            {
                return string.Join(" ", errors.Select(Loc.TS));
            }
        }
        catch
        {
            // fall through to the status code
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => Loc.T("Zaloguj się ponownie."),
            System.Net.HttpStatusCode.Forbidden => Loc.T("Brak uprawnień do tej operacji."),
            System.Net.HttpStatusCode.NotFound => Loc.T("Nie znaleziono użytkownika."),
            _ => Loc.F("Błąd serwera ({0}).", (int)response.StatusCode)
        };
    }

    private sealed class ApiErrorPayload
    {
        public string[]? Errors { get; init; }
    }

    // ---- media -----------------------------------------------------------------------------

    public Task<GameDto?> UploadGameCoverAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/cover" : $"api/Games/{gameId}/media/cover",
            HttpMethod.Put,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> ClearGameCoverAsync(
        int gameId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/cover" : $"api/Games/{gameId}/media/cover",
            HttpMethod.Delete,
            cancellationToken);

    public Task<GameDto?> UploadGameHeaderAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/header" : $"api/Games/{gameId}/media/header",
            HttpMethod.Put,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> ClearGameHeaderAsync(
        int gameId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/header" : $"api/Games/{gameId}/media/header",
            HttpMethod.Delete,
            cancellationToken);

    public Task<GameDto?> UploadGameLogoAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/logo" : $"api/Games/{gameId}/media/logo",
            HttpMethod.Put,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> ClearGameLogoAsync(
        int gameId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/logo" : $"api/Games/{gameId}/media/logo",
            HttpMethod.Delete,
            cancellationToken);

    public Task<GameDto?> UploadGameIntroAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/intro" : $"api/Games/{gameId}/media/intro",
            HttpMethod.Put,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> ClearGameIntroAsync(
        int gameId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/intro" : $"api/Games/{gameId}/media/intro",
            HttpMethod.Delete,
            cancellationToken);

    public Task<GameDto?> AddGameScreenshotAsync(
        int gameId,
        string localFilePath,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        UploadMediaFileAsync(
            asAdmin ? $"api/admin/games/{gameId}/media/screenshots" : $"api/Games/{gameId}/media/screenshots",
            HttpMethod.Post,
            localFilePath,
            cancellationToken);

    public Task<GameDto?> RemoveGameScreenshotAsync(
        int gameId,
        int screenshotId,
        bool asAdmin,
        CancellationToken cancellationToken = default) =>
        SendMediaAsync(
            asAdmin
                ? $"api/admin/games/{gameId}/media/screenshots/{screenshotId}"
                : $"api/Games/{gameId}/media/screenshots/{screenshotId}",
            HttpMethod.Delete,
            cancellationToken);

    // ---- build versions / stats ------------------------------------------------------------

    public Task<IReadOnlyList<GameBuildVersionDto>> GetBuildVersionsAsync(
        int gameId,
        int buildId,
        CancellationToken cancellationToken = default) =>
        SendListAsync<GameBuildVersionDto>($"api/Games/{gameId}/builds/{buildId}/versions", cancellationToken);

    public Task<IReadOnlyList<GameBuildVersionDto>> GetOwnedVersionsAsync(
        int gameId,
        CancellationToken cancellationToken = default) =>
        SendListAsync<GameBuildVersionDto>($"api/Games/{gameId}/studio/versions", cancellationToken);

    public Task<GameStatsDto?> GetOwnedGameStatsAsync(
        int gameId,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameStatsDto>(HttpMethod.Get, $"api/Games/{gameId}/stats", null, cancellationToken);

    public Task<GameBuildVersionDto?> UpdateVersionNotesAsync(
        int gameId,
        int versionId,
        string? releaseNotes,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameBuildVersionDto>(
            HttpMethod.Patch,
            $"api/Games/{gameId}/versions/{versionId}/notes",
            new UpdateVersionNotesRequestDto { ReleaseNotes = releaseNotes },
            cancellationToken);

    // ---- achievements ----------------------------------------------------------------------

    public Task<IReadOnlyList<AchievementDefinitionDto>> GetAchievementDefinitionsAsync(
        int gameId,
        CancellationToken cancellationToken = default) =>
        SendListAsync<AchievementDefinitionDto>($"api/games/{gameId}/studio/achievements", cancellationToken);

    public async Task<(IReadOnlyList<AchievementDefinitionDto>? Items, string? Error)> SyncAchievementDefinitionsAsync(
        int gameId,
        IReadOnlyList<AchievementDefinitionDto> items,
        CancellationToken cancellationToken = default)
    {
        var (list, error) = await SendWithErrorAsync<List<AchievementDefinitionDto>>(
            HttpMethod.Put,
            $"api/games/{gameId}/studio/achievements",
            new SyncGameAchievementsRequest { Items = items },
            cancellationToken).ConfigureAwait(false);
        return (list, error);
    }

    public Task<(AchievementUpdateResultDto? Result, string? Error)> TestUnlockAchievementAsync(
        int gameId,
        string key,
        CancellationToken cancellationToken = default) =>
        SendWithErrorAsync<AchievementUpdateResultDto>(
            HttpMethod.Post,
            $"api/me/games/{gameId}/achievements/{Uri.EscapeDataString(key)}/unlock",
            body: null,
            cancellationToken);

    public Task<bool> ResetMyAchievementProgressAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/games/{gameId}/studio/achievements/progress", body: null, cancellationToken);

    public async Task<(AchievementDefinitionDto? Item, string? Error)> UploadAchievementIconAsync(
        int gameId,
        string key,
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
        {
            return (null, Loc.T("Nie można odczytać wybranego pliku."));
        }

        try
        {
            await using var stream = File.OpenRead(localFilePath);
            using var content = new MultipartFormDataContent
            {
                { new StreamContent(stream), "file", Path.GetFileName(localFilePath) }
            };
            using var request = new HttpRequestMessage(
                HttpMethod.Put,
                $"api/games/{gameId}/studio/achievements/{Uri.EscapeDataString(key)}/icon")
            { Content = content };
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false));
            }

            return (await response.Content.ReadFromJsonAsync<AchievementDefinitionDto>(cancellationToken)
                .ConfigureAwait(false), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public Task<GameAchievementsDto?> GetGameAchievementsAsync(
        int gameId,
        string? language = null,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameAchievementsDto>(
            HttpMethod.Get,
            $"api/games/{gameId}/achievements"
            + (string.IsNullOrWhiteSpace(language) ? string.Empty : $"?lang={Uri.EscapeDataString(language)}"),
            body: null,
            cancellationToken);

    public Task<IReadOnlyList<GameBuildVersionDto>> GetAdminVersionsAsync(
        int gameId,
        CancellationToken cancellationToken = default) =>
        SendListAsync<GameBuildVersionDto>($"api/admin/games/{gameId}/versions", cancellationToken);

    public Task<GameStatsDto?> GetAdminGameStatsAsync(
        int gameId,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameStatsDto>(HttpMethod.Get, $"api/admin/games/{gameId}/stats", null, cancellationToken);

    public Task<GameBuildVersionDto?> UpdateAdminVersionNotesAsync(
        int gameId,
        int versionId,
        string? releaseNotes,
        CancellationToken cancellationToken = default) =>
        SendJsonAsync<GameBuildVersionDto>(
            HttpMethod.Patch,
            $"api/admin/games/{gameId}/versions/{versionId}/notes",
            new UpdateVersionNotesRequestDto { ReleaseNotes = releaseNotes },
            cancellationToken);

    // ---- reviews ---------------------------------------------------------------------------

    public async Task<GameReviewsSummaryDto?> GetGameReviewsAsync(
        int gameId,
        string? sort = null,
        bool? recommended = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(sort))
            {
                parts.Add($"sort={Uri.EscapeDataString(sort.Trim())}");
            }

            if (recommended is bool rec)
            {
                parts.Add($"recommended={rec.ToString().ToLowerInvariant()}");
            }

            var qs = parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
            return await SendJsonAsync<GameReviewsSummaryDto>(
                HttpMethod.Get,
                $"api/Games/{gameId}/reviews{qs}",
                body: null,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public Task<(GameReviewDto? Review, string? Error)> UpsertGameReviewAsync(
        int gameId,
        UpsertGameReviewRequest request,
        CancellationToken cancellationToken = default) =>
        SendWithErrorAsync<GameReviewDto>(HttpMethod.Put, $"api/Games/{gameId}/reviews/me", request, cancellationToken);

    public Task<bool> DeleteGameReviewAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/Games/{gameId}/reviews/me", null, cancellationToken);

    public Task<bool> VoteGameReviewAsync(
        long reviewId,
        bool helpful,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            HttpMethod.Post,
            $"api/Games/reviews/{reviewId}/vote",
            new SetReviewVoteRequest { Helpful = helpful },
            cancellationToken);

    public Task<IReadOnlyList<GameReviewCommentDto>> GetGameReviewCommentsAsync(
        long reviewId,
        CancellationToken cancellationToken = default) =>
        SendListAsync<GameReviewCommentDto>($"api/Games/reviews/{reviewId}/comments", cancellationToken);

    public Task<(GameReviewCommentDto? Comment, string? Error)> AddGameReviewCommentAsync(
        long reviewId,
        string text,
        CancellationToken cancellationToken = default) =>
        SendWithErrorAsync<GameReviewCommentDto>(
            HttpMethod.Post,
            $"api/Games/reviews/{reviewId}/comments",
            new CreateGameReviewCommentRequest { Text = text },
            cancellationToken);

    public Task<bool> DeleteGameReviewCommentAsync(long commentId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/Games/reviews/comments/{commentId}", null, cancellationToken);

    private async Task<GameDto?> UploadMediaFileAsync(
        string url,
        HttpMethod method,
        string localFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SGS Media] UploadMediaFile missing path url={url} path={localFilePath}");
                return null;
            }

            await using var stream = File.OpenRead(localFilePath);
            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(stream);
            var fileName = Path.GetFileName(localFilePath);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            });
            content.Add(fileContent, "file", fileName);

            using var message = new HttpRequestMessage(method, url) { Content = content };
            System.Diagnostics.Debug.WriteLine(
                $"[SGS Media] HTTP {(method.Method)} {url} file={fileName} bytes={new FileInfo(localFilePath).Length}");
            using var response = await http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine(
                    $"[SGS Media] HTTP {(int)response.StatusCode} {response.ReasonPhrase} body={Truncate(body, 500)}");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"[SGS Media] HTTP {(int)response.StatusCode} OK");
            return await response.Content.ReadFromJsonAsync<GameDto>(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SGS Media] UploadMediaFile exception: {ex}");
            return null;
        }
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "(empty)";
        }

        return value.Length <= max ? value : value[..max] + "…";
    }

    private Task<GameDto?> SendMediaAsync(
        string url,
        HttpMethod method,
        CancellationToken cancellationToken) =>
        SendJsonAsync<GameDto>(method, url, null, cancellationToken);

    // ---- creative content (artworks, tracks, stories) ---------------------------------------

    private static string BuildCreativeUrl(string route, CreativeQuery? query)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query?.Q)) parts.Add("q=" + Uri.EscapeDataString(query.Q.Trim()));
        if (!string.IsNullOrWhiteSpace(query?.Tag)) parts.Add("tag=" + Uri.EscapeDataString(query.Tag.Trim()));
        if (!string.IsNullOrWhiteSpace(query?.Sort)) parts.Add("sort=" + Uri.EscapeDataString(query.Sort.Trim()));
        return parts.Count == 0 ? route : route + "?" + string.Join("&", parts);
    }

    public Task<IReadOnlyList<ArtworkDto>> GetArtworksAsync(CreativeQuery? query = null, CancellationToken cancellationToken = default) =>
        SendListAsync<ArtworkDto>(BuildCreativeUrl("api/art", query), cancellationToken);

    public Task<IReadOnlyList<TrackDto>> GetTracksAsync(CreativeQuery? query = null, CancellationToken cancellationToken = default) =>
        SendListAsync<TrackDto>(BuildCreativeUrl("api/music", query), cancellationToken);

    public Task<IReadOnlyList<StoryDto>> GetStoriesAsync(CreativeQuery? query = null, CancellationToken cancellationToken = default) =>
        SendListAsync<StoryDto>(BuildCreativeUrl("api/stories", query), cancellationToken);

    public Task<StoryDto?> GetStoryAsync(int id, CancellationToken cancellationToken = default) =>
        SendJsonAsync<StoryDto>(HttpMethod.Get, $"api/stories/{id}", body: null, cancellationToken);

    public Task<(ArtworkDto? Item, IReadOnlyList<string> Errors)> UploadArtworkAsync(
        string title,
        string? description,
        IReadOnlyList<string> tags,
        string localFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) =>
        PostCreativeAsync<ArtworkDto>("api/art", title, description, tags, localFilePath, null, progress, cancellationToken);

    public Task<(TrackDto? Item, IReadOnlyList<string> Errors)> UploadTrackAsync(
        string title,
        string? description,
        IReadOnlyList<string> tags,
        string localFilePath,
        string? coverFilePath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default) =>
        PostCreativeAsync<TrackDto>("api/music", title, description, tags, localFilePath, coverFilePath, progress, cancellationToken);

    public async Task<(StoryDto? Item, IReadOnlyList<string> Errors)> CreateStoryAsync(
        CreateStoryRequestDto request,
        string? coverFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var create = CreateRequest(HttpMethod.Post, "api/stories", request);
            using var response = await http.SendAsync(create, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, await ReadErrorsAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var story = await response.Content.ReadFromJsonAsync<StoryDto>(cancellationToken).ConfigureAwait(false);
            if (story is null)
            {
                return (null, [Loc.T("Serwer nie zwrócił danych.")]);
            }

            if (!string.IsNullOrWhiteSpace(coverFilePath))
            {
                if (!File.Exists(coverFilePath))
                {
                    return (story, [Loc.T("Nie można odczytać wybranego pliku.")]);
                }

                await using var stream = File.OpenRead(coverFilePath);
                using var content = new MultipartFormDataContent
                {
                    { new StreamContent(stream), "cover", Path.GetFileName(coverFilePath) }
                };
                using var coverRequest = new HttpRequestMessage(HttpMethod.Post, $"api/stories/{story.Id}/cover") { Content = content };
                using var coverResponse = await http.SendAsync(coverRequest, cancellationToken).ConfigureAwait(false);
                if (!coverResponse.IsSuccessStatusCode)
                {
                    // the story exists already: hand it back together with the cover error
                    return (story, await ReadErrorsAsync(coverResponse, cancellationToken).ConfigureAwait(false));
                }
            }

            return (story, []);
        }
        catch (Exception ex)
        {
            return (null, [ex.Message]);
        }
    }

    private async Task<(T? Item, IReadOnlyList<string> Errors)> PostCreativeAsync<T>(
        string url,
        string title,
        string? description,
        IReadOnlyList<string> tags,
        string filePath,
        string? coverPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)
            || (!string.IsNullOrWhiteSpace(coverPath) && !File.Exists(coverPath)))
        {
            return (null, [Loc.T("Nie można odczytać wybranego pliku.")]);
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            using var content = new MultipartFormDataContent
            {
                { new StringContent(title), "title" },
                { new StringContent(description ?? string.Empty), "description" },
                { new StringContent(string.Join(",", tags)), "tags" },
                { new ProgressStreamContent(stream, progress), "file", Path.GetFileName(filePath) }
            };
            await using var coverStream = string.IsNullOrWhiteSpace(coverPath) ? null : File.OpenRead(coverPath);
            if (coverStream is not null)
            {
                content.Add(new StreamContent(coverStream), "cover", Path.GetFileName(coverPath!));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, await ReadErrorsAsync(response, cancellationToken).ConfigureAwait(false));
            }

            progress?.Report(1);
            var item = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            return item is null ? (null, [Loc.T("Serwer nie zwrócił danych.")]) : (item, []);
        }
        catch (Exception ex)
        {
            return (null, [ex.Message]);
        }
    }

    /// <summary>Server "errors" array (or the status text) as a list.</summary>
    private static async Task<IReadOnlyList<string>> ReadErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiErrorPayload>(cancellationToken).ConfigureAwait(false);
            if (payload?.Errors is { Length: > 0 } errors)
            {
                return errors.Select(Loc.TS).ToArray();
            }
        }
        catch
        {
            // fall through to the status code
        }

        return [await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false)];
    }

    /// <summary>Streams a file and reports bytes sent as 0..1 (held below 1 until the server answers).</summary>
    private sealed class ProgressStreamContent(Stream source, IProgress<double>? progress) : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
        {
            var buffer = new byte[81920];
            long sent = 0;
            int read;
            while ((read = await source.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                await stream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                sent += read;
                progress?.Report(source.Length == 0 ? 0 : Math.Min(0.99, (double)sent / source.Length));
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = source.Length;
            return true;
        }
    }

    // ---- creative content: own works, admin lists, editing, comments --------------------------

    public Task<IReadOnlyList<ArtworkDto>> GetMyArtworksAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<ArtworkDto>("api/art/mine", cancellationToken);

    public Task<IReadOnlyList<TrackDto>> GetMyTracksAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<TrackDto>("api/music/mine", cancellationToken);

    public Task<IReadOnlyList<StoryDto>> GetMyStoriesAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<StoryDto>("api/stories/mine", cancellationToken);

    public Task<IReadOnlyList<ArtworkDto>> GetAdminArtworksAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<ArtworkDto>("api/admin/art", cancellationToken);

    public Task<IReadOnlyList<TrackDto>> GetAdminTracksAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<TrackDto>("api/admin/music", cancellationToken);

    public Task<IReadOnlyList<StoryDto>> GetAdminStoriesAsync(CancellationToken cancellationToken = default) =>
        SendListAsync<StoryDto>("api/admin/stories", cancellationToken);

    public async Task<(ArtworkDto? Item, IReadOnlyList<string> Errors)> UpdateArtworkAsync(
        int id, UpdateCreativeRequestDto request, CancellationToken cancellationToken = default)
    {
        var (item, errors, _) = await PutCreativeAsync<ArtworkDto>($"api/art/{id}", request, null, null, cancellationToken).ConfigureAwait(false);
        return (item, errors);
    }

    public async Task<(TrackDto? Item, IReadOnlyList<string> Errors)> UpdateTrackAsync(
        int id, UpdateCreativeRequestDto request, string? newCoverFilePath, CancellationToken cancellationToken = default)
    {
        var (item, errors, _) = await PutCreativeAsync<TrackDto>($"api/music/{id}", request, $"api/music/{id}/cover", newCoverFilePath, cancellationToken).ConfigureAwait(false);
        return (item, errors);
    }

    public async Task<(StoryDto? Item, IReadOnlyList<string> Errors)> UpdateStoryAsync(
        int id, UpdateStoryRequestDto request, string? newCoverFilePath, CancellationToken cancellationToken = default)
    {
        var (item, errors, _) = await PutCreativeAsync<StoryDto>($"api/stories/{id}", request, $"api/stories/{id}/cover", newCoverFilePath, cancellationToken).ConfigureAwait(false);
        return (item, errors);
    }

    /// <summary>PUT the JSON body, then the optional multipart cover; a cover failure returns the saved item with the errors.</summary>
    private async Task<(T? Item, IReadOnlyList<string> Errors, bool Saved)> PutCreativeAsync<T>(
        string url, object body, string? coverUrl, string? coverPath, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var put = CreateRequest(HttpMethod.Put, url, body);
            using var response = await http.SendAsync(put, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, await ReadErrorsAsync(response, cancellationToken).ConfigureAwait(false), false);
            }

            var item = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            if (item is null)
            {
                return (null, [Loc.T("Serwer nie zwrócił danych.")], true);
            }

            if (coverUrl is null || string.IsNullOrWhiteSpace(coverPath))
            {
                return (item, [], true);
            }

            if (!File.Exists(coverPath))
            {
                return (item, [Loc.T("Nie można odczytać wybranego pliku.")], true);
            }

            await using var stream = File.OpenRead(coverPath);
            using var content = new MultipartFormDataContent
            {
                { new StreamContent(stream), "cover", Path.GetFileName(coverPath) }
            };
            using var coverRequest = new HttpRequestMessage(HttpMethod.Post, coverUrl) { Content = content };
            using var coverResponse = await http.SendAsync(coverRequest, cancellationToken).ConfigureAwait(false);
            if (!coverResponse.IsSuccessStatusCode)
            {
                return (item, await ReadErrorsAsync(coverResponse, cancellationToken).ConfigureAwait(false), true);
            }

            // the cover changed server side: re-read so CoverUrl is current
            var fresh = await SendJsonAsync<T>(HttpMethod.Get, url, null, cancellationToken).ConfigureAwait(false);
            return (fresh ?? item, [], true);
        }
        catch (Exception ex)
        {
            return (null, [ex.Message], false);
        }
    }

    public Task<bool> DeleteCreativeAsync(string section, int id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/{section}/{id}", null, cancellationToken);

    public Task<IReadOnlyList<CreativeCommentDto>> GetCreativeCommentsAsync(string section, int itemId, CancellationToken cancellationToken = default) =>
        SendListAsync<CreativeCommentDto>($"api/{section}/{itemId}/comments", cancellationToken);

    public async Task<(CreativeCommentDto? Item, IReadOnlyList<string> Errors)> AddCreativeCommentAsync(
        string section, int itemId, string text, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Post, $"api/{section}/{itemId}/comments", new AddCreativeCommentRequestDto { Text = text });
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, await ReadErrorsAsync(response, cancellationToken).ConfigureAwait(false));
            }

            var item = await response.Content.ReadFromJsonAsync<CreativeCommentDto>(cancellationToken).ConfigureAwait(false);
            return item is null ? (null, [Loc.T("Serwer nie zwrócił danych.")]) : (item, []);
        }
        catch (Exception ex)
        {
            return (null, [ex.Message]);
        }
    }

    public Task<bool> DeleteCreativeCommentAsync(string section, int itemId, long commentId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"api/{section}/{itemId}/comments/{commentId}", null, cancellationToken);
}
