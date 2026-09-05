using Microsoft.EntityFrameworkCore;
using SGSClient.Core.Auth;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence.Services;

public sealed class EfUserService(SgsServerDbContext db, IAdminAuthorization adminAuth) : IUserService
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
        var normalizedEmail = EmailClaimHelper.Normalize(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.AzureSub == azureSub, cancellationToken);

        if (user is null)
        {
            user = new UserEntity
            {
                AzureSub = azureSub,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName,
                Email = normalizedEmail,
                AvatarUrl = avatarUrl,
                Role = adminAuth.IsBootstrapEmail(normalizedEmail) ? UserRole.SuperAdmin : UserRole.User,
                CreatedAtUtc = now,
                LastLoginUtc = now
            };
            db.Users.Add(user);
        }
        else
        {
            // DisplayName / PlayerTag are client-owned after the first login.
            if (normalizedEmail is not null)
            {
                user.Email = normalizedEmail;
            }

            // Keep uploaded/custom AvatarUrl — only fill from IdP when empty.
            if (string.IsNullOrWhiteSpace(user.AvatarUrl) && !string.IsNullOrWhiteSpace(avatarUrl))
            {
                user.AvatarUrl = avatarUrl;
            }

            if (adminAuth.IsBootstrapEmail(user.Email) && user.Role != UserRole.SuperAdmin)
            {
                user.Role = UserRole.SuperAdmin;
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

    public async Task<UserProfileDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
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

        if (request.Email is not null)
        {
            user.Email = EmailClaimHelper.Normalize(request.Email);
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

        if (request.PlayerTag is not null)
        {
            await AssignPlayerTagAsync(user, request.PlayerTag, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserProfileDto?> ClaimPlayerTagAsync(
        string azureSub,
        string tag,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(azureSub);

        var user = await db.Users.FirstOrDefaultAsync(u => u.AzureSub == azureSub, cancellationToken);
        if (user is null)
        {
            return null;
        }

        await AssignPlayerTagAsync(user, tag, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<PlayerTagAvailabilityDto> CheckPlayerTagAsync(
        string tag,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!PlayerTagRules.TryNormalize(tag, out var normalized, out var error))
        {
            return new PlayerTagAvailabilityDto { Available = false, Error = error };
        }

        var taken = await db.Users.AnyAsync(
            u => u.PlayerTag == normalized && (excludeUserId == null || u.Id != excludeUserId),
            cancellationToken);

        return taken
            ? new PlayerTagAvailabilityDto { Available = false, Normalized = normalized, Error = "Ten tag jest już zajęty." }
            : new PlayerTagAvailabilityDto { Available = true, Normalized = normalized };
    }

    private async Task AssignPlayerTagAsync(UserEntity user, string tag, CancellationToken cancellationToken)
    {
        var check = await CheckPlayerTagAsync(tag, user.Id, cancellationToken);
        if (!check.Available || string.IsNullOrWhiteSpace(check.Normalized))
        {
            throw new ArgumentException(check.Error ?? "Nie można ustawić tagu.");
        }

        user.PlayerTag = check.Normalized;
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        return users.Select(MapAdmin).ToList();
    }

    public async Task<AdminUserDto?> SetUserRoleAsync(
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (adminAuth.IsBootstrapEmail(user.Email))
        {
            throw new InvalidOperationException("Konto właściciela ma stałą rolę superadmina.");
        }

        user.Role = role;
        await db.SaveChangesAsync(cancellationToken);
        return MapAdmin(user);
    }

    public async Task TouchPresenceAsync(string azureSub, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(azureSub))
        {
            return;
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.AzureSub == azureSub, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.LastSeenUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private AdminUserDto MapAdmin(UserEntity user) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        Email = user.Email,
        PlayerTag = user.PlayerTag,
        Role = adminAuth.ResolveRole(user.Role, user.Email),
        IsRoleLocked = adminAuth.IsBootstrapEmail(user.Email),
        CreatedAtUtc = user.CreatedAtUtc,
        LastLoginUtc = user.LastLoginUtc
    };

    private UserProfileDto Map(UserEntity user) => new()
    {
        Id = user.Id,
        AzureSub = user.AzureSub,
        DisplayName = user.DisplayName,
        Email = user.Email,
        AvatarUrl = user.AvatarUrl,
        Bio = user.Bio,
        Location = user.Location,
        PlayerTag = user.PlayerTag,
        Role = adminAuth.ResolveRole(user.Role, user.Email),
        CreatedAtUtc = user.CreatedAtUtc,
        LastLoginUtc = user.LastLoginUtc
    };
}
