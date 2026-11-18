using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IUserApiClient
{
    Task<UserProfileDto?> GetMeAsync(CancellationToken cancellationToken = default);
    Task<UserProfileDto?> GetMeWithIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> UpdateMeAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<(UserProfileDto? Profile, string? Error)> UploadAvatarAsync(string localFilePath, CancellationToken cancellationToken = default);
    Task<(UserProfileDto? Profile, string? Error)> UploadAvatarAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> ClearAvatarAsync(CancellationToken cancellationToken = default);

    Task<(UserProfileDto? Profile, string? Error)> ClaimPlayerTagAsync(
        string tag,
        CancellationToken cancellationToken = default);

    Task<PlayerTagAvailabilityDto?> CheckPlayerTagAsync(
        string tag,
        CancellationToken cancellationToken = default);

    Task<SocialOverviewDto?> GetSocialAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialUserDto>> SearchUsersAsync(string query, CancellationToken cancellationToken = default);

    Task<(SocialOverviewDto? Overview, string? Error)> SendFriendRequestAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<(SocialOverviewDto? Overview, string? Error)> AcceptFriendRequestAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<(SocialOverviewDto? Overview, string? Error)> RemoveFriendAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<(SocialOverviewDto? Overview, string? Error)> FollowAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<(SocialOverviewDto? Overview, string? Error)> UnfollowAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<PublicUserProfileDto?> GetPublicProfileAsync(int userId, CancellationToken cancellationToken = default);

    Task<PublicUserProfileDto?> GetPublicProfileByTagAsync(string tag, CancellationToken cancellationToken = default);

    Task<(ProfileCommentDto? Comment, string? Error)> AddProfileCommentAsync(
        int userId,
        string text,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> DeleteProfileCommentAsync(
        int userId,
        long commentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatThreadDto>> GetChatThreadsAsync(CancellationToken cancellationToken = default);

    Task<(LobbyDto? Lobby, string? Error)> CreateLobbyAsync(
        CreateLobbyRequest request,
        CancellationToken cancellationToken = default);

    Task<LobbyDto?> GetLobbyAsync(string code, CancellationToken cancellationToken = default);

    Task<(LobbyDto? Lobby, string? Error)> JoinLobbyAsync(string code, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> LeaveLobbyAsync(string code, CancellationToken cancellationToken = default);

    Task<(LobbyDto? Lobby, string? Error)> StartLobbyAsync(string code, CancellationToken cancellationToken = default);

    Task<(LobbyDto? Lobby, string? Error)> SetLobbyReadyAsync(
        string code,
        bool ready,
        CancellationToken cancellationToken = default);

    Task<(LobbyDto? Lobby, string? Error)> KickFromLobbyAsync(
        string code,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>Host only: the match is over and the party is back in the lobby.</summary>
    Task<(LobbyDto? Lobby, string? Error)> FinishLobbyAsync(string code, CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> InviteToLobbyAsync(
        string code,
        int friendUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvitableFriendDto>> GetInvitableFriendsAsync(
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>PATCH with only the changed fields; an empty request just keeps the lobby alive.</summary>
    Task<(LobbyDto? Lobby, string? Error)> UpdateLobbyAsync(
        string code,
        UpdateLobbyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Open lobbies with a free slot that friends host.</summary>
    Task<IReadOnlyList<LobbyDto>> GetFriendLobbiesAsync(CancellationToken cancellationToken = default);

    Task<ChatConversationDto?> GetChatConversationAsync(int peerUserId, CancellationToken cancellationToken = default);

    Task<(ChatMessageDto? Message, string? Error)> SendChatMessageAsync(
        int peerUserId,
        string text,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadNotificationCountAsync(CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> MarkNotificationReadAsync(
        long notificationId,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> MarkAllNotificationsReadAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlaytimeDto>> GetPlaytimeAsync(CancellationToken cancellationToken = default);

    Task<PlaytimeDto?> GetPlaytimeAsync(int gameId, CancellationToken cancellationToken = default);

    Task<PlaytimeDto?> RecordPlaytimeSessionAsync(
        RecordPlaytimeSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Per game: how many achievements the signed-in player has unlocked.</summary>
    Task<IReadOnlyList<UserAchievementSummaryDto>> GetAchievementSummariesAsync(
        CancellationToken cancellationToken = default);
}
