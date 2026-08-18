using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IUserApiClient
{
    Task<UserProfileDto?> GetMeAsync(CancellationToken cancellationToken = default);
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

    Task<IReadOnlyList<PlaytimeDto>> GetPlaytimeAsync(CancellationToken cancellationToken = default);

    Task<PlaytimeDto?> GetPlaytimeAsync(int gameId, CancellationToken cancellationToken = default);

    Task<PlaytimeDto?> RecordPlaytimeSessionAsync(
        RecordPlaytimeSessionRequest request,
        CancellationToken cancellationToken = default);
}
