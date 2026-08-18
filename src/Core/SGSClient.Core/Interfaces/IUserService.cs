using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IUserService
{
    Task<UserProfileDto> UpsertFromClaimsAsync(
        string azureSub,
        string? displayName,
        string? email,
        string? avatarUrl,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetByAzureSubAsync(string azureSub, CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<UserProfileDto?> UpdateProfileAsync(
        string azureSub,
        UpdateProfileRequestDto request,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto?> ClaimPlayerTagAsync(
        string azureSub,
        string tag,
        CancellationToken cancellationToken = default);

    Task<PlayerTagAvailabilityDto> CheckPlayerTagAsync(
        string tag,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminUserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
}
