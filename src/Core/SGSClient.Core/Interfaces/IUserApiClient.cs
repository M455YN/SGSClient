using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IUserApiClient
{
    Task<UserProfileDto?> GetMeAsync(CancellationToken cancellationToken = default);
    Task<UserProfileDto?> UpdateMeAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken = default);
    Task<(UserProfileDto? Profile, string? Error)> UploadAvatarAsync(string localFilePath, CancellationToken cancellationToken = default);
    Task<(UserProfileDto? Profile, string? Error)> UploadAvatarAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<UserProfileDto?> ClearAvatarAsync(CancellationToken cancellationToken = default);
}
