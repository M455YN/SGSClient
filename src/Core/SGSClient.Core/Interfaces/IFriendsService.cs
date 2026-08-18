using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IFriendsService
{
    Task<SocialOverviewDto> GetOverviewAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialUserDto>> SearchUsersAsync(
        int userId,
        string query,
        CancellationToken cancellationToken = default);

    Task<SocialOverviewDto> SendFriendRequestAsync(
        int userId,
        int targetUserId,
        CancellationToken cancellationToken = default);

    Task<SocialOverviewDto> AcceptFriendRequestAsync(
        int userId,
        int fromUserId,
        CancellationToken cancellationToken = default);

    Task<SocialOverviewDto> RemoveFriendAsync(
        int userId,
        int otherUserId,
        CancellationToken cancellationToken = default);

    Task<SocialOverviewDto> FollowAsync(
        int userId,
        int targetUserId,
        CancellationToken cancellationToken = default);

    Task<SocialOverviewDto> UnfollowAsync(
        int userId,
        int targetUserId,
        CancellationToken cancellationToken = default);
}
