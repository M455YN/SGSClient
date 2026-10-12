using SGSClient.Core.Localization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;

namespace SGSClient.Infrastructure.Client.Auth;

public sealed class BearerUserApiClient(HttpClient http, IAuthSession authSession) : IUserApiClient
{
    public Task<UserProfileDto?> GetMeAsync(CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Get, "api/me", body: null, cancellationToken);

    public Task<UserProfileDto?> UpdateMeAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Patch, "api/me", request, cancellationToken);

    public async Task<(UserProfileDto? Profile, string? Error)> UploadAvatarAsync(
        string localFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localFilePath) || !File.Exists(localFilePath))
        {
            return (null, Loc.T("Nie można odczytać wybranego pliku."));
        }

        await using var stream = File.OpenRead(localFilePath);
        return await UploadAvatarAsync(stream, Path.GetFileName(localFilePath), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(UserProfileDto? Profile, string? Error)> UploadAvatarAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, Loc.T("Brak tokena — zaloguj się ponownie."));
        }

        if (content is null || string.IsNullOrWhiteSpace(fileName))
        {
            return (null, Loc.T("Brak pliku zdjęcia."));
        }

        try
        {
            // WinRT / non-seekable streams break multipart; buffer first.
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (buffer.Length == 0)
            {
                return (null, Loc.T("Wybrany plik jest pusty."));
            }

            buffer.Position = 0;

            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(buffer);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            });
            form.Add(fileContent, "file", fileName);

            using var request = new HttpRequestMessage(HttpMethod.Put, "api/me/avatar") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var detail = TryExtractError(body);
                return (null, $"HTTP {(int)response.StatusCode}: {detail ?? response.ReasonPhrase ?? "błąd"}");
            }

            var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>(cancellationToken)
                .ConfigureAwait(false);
            return profile is null
                ? (null, Loc.T("API nie zwróciło profilu."))
                : (profile, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public Task<UserProfileDto?> ClearAvatarAsync(CancellationToken cancellationToken = default) =>
        SendAsync<UserProfileDto>(HttpMethod.Delete, "api/me/avatar", body: null, cancellationToken);

    public Task<(UserProfileDto? Profile, string? Error)> ClaimPlayerTagAsync(
        string tag,
        CancellationToken cancellationToken = default) =>
        SendResultAsync<UserProfileDto>(
            HttpMethod.Put,
            "api/me/tag",
            new ClaimPlayerTagRequestDto { Tag = tag },
            cancellationToken);

    public async Task<PlayerTagAvailabilityDto?> CheckPlayerTagAsync(
        string tag,
        CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(tag ?? string.Empty);
        return await SendAsync<PlayerTagAvailabilityDto>(
                HttpMethod.Get,
                $"api/me/tag-available?q={encoded}",
                body: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<SocialOverviewDto?> GetSocialAsync(CancellationToken cancellationToken = default) =>
        SendAsync<SocialOverviewDto>(HttpMethod.Get, "api/me/social", body: null, cancellationToken);

    public async Task<IReadOnlyList<SocialUserDto>> SearchUsersAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString(query ?? string.Empty);
        var result = await SendAsync<List<SocialUserDto>>(
                HttpMethod.Get,
                $"api/users/search?q={encoded}",
                body: null,
                cancellationToken)
            .ConfigureAwait(false);
        return result ?? [];
    }

    public Task<(SocialOverviewDto? Overview, string? Error)> SendFriendRequestAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        SendResultAsync<SocialOverviewDto>(HttpMethod.Post, $"api/me/friends/{userId}", body: null, cancellationToken);

    public Task<(SocialOverviewDto? Overview, string? Error)> AcceptFriendRequestAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        SendResultAsync<SocialOverviewDto>(
            HttpMethod.Post,
            $"api/me/friends/{userId}/accept",
            body: null,
            cancellationToken);

    public Task<(SocialOverviewDto? Overview, string? Error)> RemoveFriendAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        SendResultAsync<SocialOverviewDto>(HttpMethod.Delete, $"api/me/friends/{userId}", body: null, cancellationToken);

    public Task<(SocialOverviewDto? Overview, string? Error)> FollowAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        SendResultAsync<SocialOverviewDto>(HttpMethod.Put, $"api/me/follows/{userId}", body: null, cancellationToken);

    public Task<(SocialOverviewDto? Overview, string? Error)> UnfollowAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        SendResultAsync<SocialOverviewDto>(HttpMethod.Delete, $"api/me/follows/{userId}", body: null, cancellationToken);

    public Task<PublicUserProfileDto?> GetPublicProfileAsync(int userId, CancellationToken cancellationToken = default) =>
        SendAllowAnonymousAsync<PublicUserProfileDto>(HttpMethod.Get, $"api/users/{userId}", body: null, cancellationToken);

    public Task<PublicUserProfileDto?> GetPublicProfileByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var encoded = Uri.EscapeDataString((tag ?? string.Empty).Trim().TrimStart('@'));
        return SendAllowAnonymousAsync<PublicUserProfileDto>(
            HttpMethod.Get,
            $"api/users/by-tag/{encoded}",
            body: null,
            cancellationToken);
    }

    public Task<(ProfileCommentDto? Comment, string? Error)> AddProfileCommentAsync(
        int userId,
        string text,
        CancellationToken cancellationToken = default) =>
        SendResultAsync<ProfileCommentDto>(
            HttpMethod.Post,
            $"api/users/{userId}/comments",
            new CreateProfileCommentRequest { Text = text },
            cancellationToken);

    public async Task<(bool Ok, string? Error)> DeleteProfileCommentAsync(
        int userId,
        long commentId,
        CancellationToken cancellationToken = default)
    {
        var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, Loc.T("Zaloguj się, żeby usunąć komentarz."));
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/users/{userId}/comments/{commentId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (false, TryExtractError(text) ?? response.ReasonPhrase ?? Loc.T("Nie udało się usunąć komentarza."));
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<ChatThreadDto>> GetChatThreadsAsync(CancellationToken cancellationToken = default)
    {
        var list = await SendAsync<List<ChatThreadDto>>(
            HttpMethod.Get,
            "api/me/chat/threads",
            body: null,
            cancellationToken).ConfigureAwait(false);
        return list ?? [];
    }

    public Task<ChatConversationDto?> GetChatConversationAsync(int peerUserId, CancellationToken cancellationToken = default) =>
        SendAsync<ChatConversationDto>(HttpMethod.Get, $"api/me/chat/{peerUserId}", body: null, cancellationToken);

    public Task<(ChatMessageDto? Message, string? Error)> SendChatMessageAsync(
        int peerUserId,
        string text,
        CancellationToken cancellationToken = default) =>
        SendResultAsync<ChatMessageDto>(
            HttpMethod.Post,
            $"api/me/chat/{peerUserId}",
            new SendChatMessageRequest { Text = text },
            cancellationToken);

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var list = await SendAsync<List<NotificationDto>>(
            HttpMethod.Get,
            $"api/me/notifications?take={take}",
            body: null,
            cancellationToken).ConfigureAwait(false);
        return list ?? [];
    }

    public async Task<int> GetUnreadNotificationCountAsync(CancellationToken cancellationToken = default)
    {
        var dto = await SendAsync<UnreadCountDto>(
            HttpMethod.Get,
            "api/me/notifications/unread-count",
            body: null,
            cancellationToken).ConfigureAwait(false);
        return dto?.Count ?? 0;
    }

    public async Task<(bool Ok, string? Error)> MarkNotificationReadAsync(
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, Loc.T("Brak tokena."));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/me/notifications/{notificationId}/read");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (false, TryExtractError(body) ?? response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Ok, string? Error)> MarkAllNotificationsReadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, Loc.T("Brak tokena."));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/me/notifications/read-all");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return (true, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (false, TryExtractError(body) ?? response.ReasonPhrase);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<PlaytimeDto>> GetPlaytimeAsync(CancellationToken cancellationToken = default)
    {
        var list = await SendAllowAnonymousAsync<List<PlaytimeDto>>(
            HttpMethod.Get,
            "api/me/playtime",
            body: null,
            cancellationToken);
        return list ?? [];
    }

    public Task<PlaytimeDto?> GetPlaytimeAsync(int gameId, CancellationToken cancellationToken = default) =>
        SendAllowAnonymousAsync<PlaytimeDto>(HttpMethod.Get, $"api/me/playtime/{gameId}", body: null, cancellationToken);

    public Task<PlaytimeDto?> RecordPlaytimeSessionAsync(
        RecordPlaytimeSessionRequest request,
        CancellationToken cancellationToken = default) =>
        SendAllowAnonymousAsync<PlaytimeDto>(HttpMethod.Post, "api/me/playtime/sessions", request, cancellationToken);

    public async Task<IReadOnlyList<UserAchievementSummaryDto>> GetAchievementSummariesAsync(
        CancellationToken cancellationToken = default) =>
        await SendAllowAnonymousAsync<List<UserAchievementSummaryDto>>(
            HttpMethod.Get,
            "api/me/achievements",
            body: null,
            cancellationToken) ?? [];

    private sealed class UnreadCountDto
    {
        public int Count { get; set; }
    }

    private static string? TryExtractError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                if (errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
                {
                    return Loc.TS(errors[0].GetString());
                }

                if (errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in errors.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                        {
                            return Loc.TS(prop.Value[0].GetString());
                        }
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("title", out var title))
            {
                return Loc.TS(title.GetString());
            }
        }
        catch
        {
            // fall through
        }

        return body.Length > 180 ? body[..180] : body;
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return default;
        }

        return await SendCoreAsync<T>(method, url, body, token, cancellationToken).ConfigureAwait(false);
    }

    private Task<T?> SendAllowAnonymousAsync<T>(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken) =>
        SendCoreAsync<T>(method, url, body, token: null, cancellationToken);

    private async Task<T?> SendCoreAsync<T>(
        HttpMethod method,
        string url,
        object? body,
        string? token,
        CancellationToken cancellationToken)
    {
        var accessToken = token ?? await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        using var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
    }

    private async Task<(T? Value, string? Error)> SendResultAsync<T>(
        HttpMethod method,
        string url,
        object? body,
        CancellationToken cancellationToken)
        where T : class
    {
        var token = await authSession.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, Loc.T("Brak tokena — zaloguj się ponownie."));
        }

        try
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"HTTP {(int)response.StatusCode}: {TryExtractError(text) ?? response.ReasonPhrase ?? "błąd"}");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return (null, Loc.T("API nie zwróciło danych."));
            }

            var value = JsonSerializer.Deserialize<T>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return value is null ? (null, Loc.T("API nie zwróciło danych.")) : (value, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
