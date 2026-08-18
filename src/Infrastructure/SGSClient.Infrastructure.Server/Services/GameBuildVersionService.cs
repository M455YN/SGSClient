using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Services;

public sealed class GameBuildVersionService(
    SgsServerDbContext db,
    IOptions<GameStorageSettings> storageOptions)
{
    public async Task RecordPublishedVersionAsync(
        GameBuildEntity build,
        string symbol,
        string version,
        string exeName,
        string zipLink,
        string fileName,
        string? releaseNotes = null,
        CancellationToken cancellationToken = default)
    {
        if (build.Id == 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await db.GameBuildVersions
            .FirstOrDefaultAsync(v => v.GameBuildId == build.Id && v.Version == version, cancellationToken)
            .ConfigureAwait(false);

        var notes = string.IsNullOrWhiteSpace(releaseNotes) ? null : releaseNotes.Trim();

        if (existing is null)
        {
            db.GameBuildVersions.Add(new GameBuildVersionEntity
            {
                GameBuildId = build.Id,
                Version = version,
                ExeName = exeName,
                ZipLink = zipLink,
                FileName = fileName,
                PublishedAtUtc = now,
                ReleaseNotes = notes
            });
        }
        else
        {
            existing.ExeName = exeName;
            existing.ZipLink = zipLink;
            existing.FileName = fileName;
            existing.PublishedAtUtc = now;

            // Keep the previous changelog when a republish omits it.
            if (notes is not null)
            {
                existing.ReleaseNotes = notes;
            }
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ApplyRetentionAsync(build, symbol, cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyRetentionAsync(
        GameBuildEntity build,
        string symbol,
        CancellationToken cancellationToken)
    {
        var retention = Math.Max(1, storageOptions.Value.RetentionCount);
        var versions = await db.GameBuildVersions
            .Where(v => v.GameBuildId == build.Id)
            .OrderByDescending(v => v.PublishedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var toRemove = versions
            .Where(v => !string.Equals(v.Version, build.CurrentVersion, StringComparison.OrdinalIgnoreCase))
            .Skip(retention - 1)
            .ToList();

        if (toRemove.Count == 0)
        {
            return;
        }

        var storageRoot = storageOptions.Value.StoragePath;
        foreach (var version in toRemove)
        {
            TryDeletePackage(storageRoot, symbol, version.Version, version.FileName);
            db.GameBuildVersions.Remove(version);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void TryDeletePackage(string storageRoot, string symbol, string version, string fileName)
    {
        try
        {
            var zipPath = Path.Combine(storageRoot, symbol, version, fileName);
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            var versionDir = Path.Combine(storageRoot, symbol, version);
            if (Directory.Exists(versionDir) && !Directory.EnumerateFileSystemEntries(versionDir).Any())
            {
                Directory.Delete(versionDir, recursive: false);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
