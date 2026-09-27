using SGSClient.Core.DTOs;

namespace SGSClient.Core.Interfaces;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> ListAsync(
        int userId,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default);

    Task<NotificationDto> CreateAsync(
        int userId,
        string kind,
        string title,
        string? body = null,
        string? dataJson = null,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(int userId, long notificationId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);
}
