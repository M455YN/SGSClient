namespace SGSClient.Core.DTOs;

public sealed class SocialOverviewDto
{
    public int FriendsCount { get; init; }
    public int FollowingCount { get; init; }
    public int FollowersCount { get; init; }
    public int IncomingCount { get; init; }
    public IReadOnlyList<SocialUserDto> Friends { get; init; } = [];
    public IReadOnlyList<SocialUserDto> Incoming { get; init; } = [];
    public IReadOnlyList<SocialUserDto> Outgoing { get; init; } = [];
    public IReadOnlyList<SocialUserDto> Following { get; init; } = [];
    public IReadOnlyList<SocialUserDto> Followers { get; init; } = [];
}
