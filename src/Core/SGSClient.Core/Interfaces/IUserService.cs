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

    Task<UserProfileDto?> UpdateProfileAsync(
        string azureSub,
        UpdateProfileRequestDto request,
        CancellationToken cancellationToken = default);
}
