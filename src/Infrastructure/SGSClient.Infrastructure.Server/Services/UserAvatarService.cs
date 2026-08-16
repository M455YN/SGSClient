using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Server.Persistence;

namespace SGSClient.Infrastructure.Server.Services;

public sealed class UserAvatarService(
    SgsServerDbContext db,
    IOptions<GameStorageSettings> storageOptions) : IUserAvatarService
{
    public const string PublicBasePath = "/api/me/avatars";
    public const long MaxAvatarBytes = 5L * 1024 * 1024; // 5 MB

    public async Task<UserProfileDto?> SetAvatarAsync(
        string azureSub,
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(azureSub);

        var user = await db.Users.FirstOrDefaultAsync(u => u.AzureSub == azureSub, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !SgsPackageRules.AllowedImageExtensions.Contains(ext))
        {
            throw new ArgumentException(
                $"Niedozwolone rozszerzenie '{ext}'. Dozwolone: {string.Join(", ", SgsPackageRules.AllowedImageExtensions)}.");
        }

        if (content.CanSeek && content.Length > MaxAvatarBytes)
        {
            throw new ArgumentException($"Avatar jest za duży (max {MaxAvatarBytes / (1024 * 1024)} MB).");
        }

        var userRoot = GetUserRoot(user.Id);
        Directory.CreateDirectory(userRoot);

        var safeName = $"avatar{ext.ToLowerInvariant()}";
        var dest = Path.Combine(userRoot, safeName);
        var previousUrl = user.AvatarUrl;

        await using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fs, cancellationToken);
            if (fs.Length > MaxAvatarBytes)
            {
                fs.Close();
                TryDeletePath(dest);
                throw new ArgumentException($"Avatar jest za duży (max {MaxAvatarBytes / (1024 * 1024)} MB).");
            }
        }

        // Remove previous avatar if extension changed.
        TryDeleteOtherAvatars(userRoot, safeName);

        user.AvatarUrl = $"{PublicBasePath}/{user.Id}/{safeName}".Replace('\\', '/');
        await db.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousUrl, user.AvatarUrl, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteByUrl(previousUrl, user.Id);
        }

        return Map(user);
    }

    public async Task<UserProfileDto?> ClearAvatarAsync(
        string azureSub,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(azureSub);

        var user = await db.Users.FirstOrDefaultAsync(u => u.AzureSub == azureSub, cancellationToken);
        if (user is null)
        {
            return null;
        }

        TryDeleteByUrl(user.AvatarUrl, user.Id);
        user.AvatarUrl = null;
        await db.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    private string GetUserRoot(int userId)
    {
        var storage = storageOptions.Value;
        return Path.GetFullPath(Path.Combine(storage.StoragePath, "users", userId.ToString()));
    }

    private void TryDeleteByUrl(string? url, int userId)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.Contains($"/avatars/{userId}/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = Path.GetFileName(url.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        var root = GetUserRoot(userId);
        var path = Path.GetFullPath(Path.Combine(root, fileName));
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            TryDeletePath(path);
        }
    }

    private static void TryDeleteOtherAvatars(string userRoot, string keepFileName)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(userRoot, "avatar.*"))
            {
                if (!string.Equals(Path.GetFileName(path), keepFileName, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeletePath(path);
                }
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static void TryDeletePath(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private static UserProfileDto Map(Persistence.Entities.UserEntity user) => new()
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
