using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfNotificationService(SgsServerDbContext db) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(
        int userId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var rows = await db.UserNotifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken cancellationToken = default) =>
        db.UserNotifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, cancellationToken);

    public async Task<NotificationDto> CreateAsync(
        int userId,
        string kind,
        string title,
        string? body = null,
        string? dataJson = null,
        CancellationToken cancellationToken = default)
    {
        var entity = new UserNotificationEntity
        {
            UserId = userId,
            Kind = kind,
            Title = title,
            Body = body,
            DataJson = dataJson,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        db.UserNotifications.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<bool> MarkReadAsync(int userId, long notificationId, CancellationToken cancellationToken = default)
    {
        var entity = await db.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        if (entity.ReadAtUtc is null)
        {
            entity.ReadAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await db.UserNotifications
            .Where(n => n.UserId == userId && n.ReadAtUtc == null)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var row in rows)
        {
            row.ReadAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static NotificationDto Map(UserNotificationEntity n) => new()
    {
        Id = n.Id,
        Kind = n.Kind,
        Title = n.Title,
        Body = n.Body,
        DataJson = n.DataJson,
        CreatedAtUtc = n.CreatedAtUtc,
        ReadAtUtc = n.ReadAtUtc
    };
}
