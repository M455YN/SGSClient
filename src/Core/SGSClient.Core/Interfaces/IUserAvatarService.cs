using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface IUserAvatarService
{
    Task<UserProfileDto?> SetAvatarAsync(
        string azureSub,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto?> ClearAvatarAsync(
        string azureSub,
        CancellationToken cancellationToken = default);
}
