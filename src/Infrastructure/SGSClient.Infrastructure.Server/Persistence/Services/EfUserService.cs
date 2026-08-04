using Microsoft.EntityFrameworkCore;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfUserService(SgsServerDbContext db) : IUserService
{
    public async Task<UserProfileDto> UpsertFromClaimsAsync(
        string azureSub,
        string? displayName,
        string? email,
        string? avatarUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(azureSub);

        var now = DateTimeOffset.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u => u.AzureSub == azureSub, cancellationToken);

        if (user is null)
        {
            user = new UserEntity
            {
                AzureSub = azureSub,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName,
                Email = email,
                AvatarUrl = avatarUrl,
                CreatedAtUtc = now,
                LastLoginUtc = now
            };
            db.Users.Add(user);
        }
        else
        {
            // Prefer claim display name / email / avatar only when local profile has not customized them yet,
            // except LastLogin which always updates. DisplayName from claims still refreshes for B2C name changes
            // unless Bio is set as a weak signal of an edited profile — keep simple: claims refresh name/email/avatar.
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                user.DisplayName = displayName;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                user.Email = email;
            }

            if (!string.IsNullOrWhiteSpace(avatarUrl))
            {
                user.AvatarUrl = avatarUrl;
            }

            user.LastLoginUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserProfileDto?> GetByAzureSubAsync(string azureSub, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.AzureSub == azureSub, cancellationToken);
        return user is null ? null : Map(user);
    }

    public async Task<UserProfileDto?> UpdateProfileAsync(
        string azureSub,
        UpdateProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(azureSub);

        var user = await db.Users.FirstOrDefaultAsync(u => u.AzureSub == azureSub, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (request.DisplayName is not null)
        {
            var name = request.DisplayName.Trim();
            if (name.Length is > 0 and <= 256)
            {
                user.DisplayName = name;
            }
        }

        if (request.Bio is not null)
        {
            user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
            if (user.Bio is { Length: > 500 })
            {
                user.Bio = user.Bio[..500];
            }
        }

        if (request.Location is not null)
        {
            user.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
            if (user.Location is { Length: > 128 })
            {
                user.Location = user.Location[..128];
            }
        }

        if (request.AvatarUrl is not null)
        {
            user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    private static UserProfileDto Map(UserEntity user) => new()
    {
        Id = user.Id,
        AzureSub = user.AzureSub,
        DisplayName = user.DisplayName,
        Email = user.Email,
        AvatarUrl = user.AvatarUrl,
        Bio = user.Bio,
        Location = user.Location,
        CreatedAtUtc = user.CreatedAtUtc,
        LastLoginUtc = user.LastLoginUtc
    };
}
