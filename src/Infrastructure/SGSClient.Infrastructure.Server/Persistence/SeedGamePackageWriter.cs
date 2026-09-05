using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence;

/// <summary>
/// Writes installable seed ZIPs under Games:StoragePath and points build ZipLinks at the API package route.
/// </summary>
public static class SeedGamePackageWriter
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task EnsurePackagesAsync(
        SgsServerDbContext db,
        IOptions<GameStorageSettings> storageOptions,
        CancellationToken cancellationToken = default)
    {
        var storage = storageOptions.Value;
        var publicBase = (storage.PublicBaseUrl ?? "/api/games/packages").TrimEnd('/');
        Directory.CreateDirectory(storage.StoragePath);

        var builds = await db.GameBuilds
            .Include(b => b.Game)
            .Where(b => b.Game != null
                && (b.Game.Symbol == "demo-runner" || b.Game.Symbol == "sky-arena"))
            .ToListAsync(cancellationToken);

        foreach (var build in builds)
        {
            var symbol = build.Game!.Symbol;
            var version = string.IsNullOrWhiteSpace(build.CurrentVersion) ? "0.1.0" : build.CurrentVersion;
            var platform = SgsPackageRules.ToManifestPlatform(build.Platform);
            var arch = string.IsNullOrWhiteSpace(build.Architecture) ? "x64" : build.Architecture!;
            var fileName = $"{platform}-{arch}.zip";
            var relativeDir = Path.Combine(symbol, version);
            var absoluteDir = Path.Combine(storage.StoragePath, relativeDir);
            Directory.CreateDirectory(absoluteDir);
            var absoluteZip = Path.Combine(absoluteDir, fileName);

            var exeRelative = ResolveSeedExecutable(build.Platform);
            if (!File.Exists(absoluteZip))
            {
                WriteSeedZip(
                    absoluteZip,
                    title: build.Game.Title,
                    symbol: symbol,
                    version: version,
                    platform: platform,
                    architecture: arch,
                    executable: exeRelative,
                    osPlatform: build.Platform);
            }

            var zipLink = $"{publicBase}/{symbol}/{version}/{fileName}".Replace('\\', '/');
            build.ExeName = exeRelative;
            build.ZipLink = zipLink;
            build.PackageSizeBytes = new FileInfo(absoluteZip).Length;

            var versionRow = await db.GameBuildVersions
                .FirstOrDefaultAsync(
                    v => v.GameBuildId == build.Id && v.Version == version,
                    cancellationToken);
            if (versionRow is null)
            {
                db.GameBuildVersions.Add(new GameBuildVersionEntity
                {
                    GameBuildId = build.Id,
                    Version = version,
                    ExeName = exeRelative,
                    ZipLink = zipLink,
                    FileName = fileName,
                    PublishedAtUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                versionRow.ExeName = exeRelative;
                versionRow.ZipLink = zipLink;
                versionRow.FileName = fileName;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string ResolveSeedExecutable(GamePlatform platform) =>
        platform switch
        {
            GamePlatform.Windows => "bin/DemoRunner.cmd",
            GamePlatform.MacOS => "bin/DemoRunner.sh",
            _ => "bin/DemoRunner.sh"
        };

    private static void WriteSeedZip(
        string zipPath,
        string title,
        string symbol,
        string version,
        string platform,
        string architecture,
        string executable,
        GamePlatform osPlatform)
    {
        var temp = zipPath + ".tmp";
        if (File.Exists(temp))
        {
            File.Delete(temp);
        }

        using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
        {
            var manifest = new SgsPackageManifest
            {
                SchemaVersion = SgsPackageManifest.CurrentSchemaVersion,
                Game = new SgsPackageGameInfo
                {
                    Title = title,
                    Symbol = symbol,
                    Kind = "game",
                    Description = $"{title} — seed package for local claim → install → play.",
                    Genre = "action",
                    Engine = "custom"
                },
                Build = new SgsPackageBuildInfo
                {
                    Version = version,
                    Platform = platform,
                    Architecture = architecture,
                    Executable = executable,
                    IsDefault = true,
                    ReleaseNotes = "Seed build"
                }
            };

            var manifestJson = JsonSerializer.Serialize(manifest, ManifestJson);
            WriteEntry(zip, "sgs-manifest.json", Encoding.UTF8.GetBytes(manifestJson));

            if (osPlatform == GamePlatform.Windows)
            {
                var cmd = """
                    @echo off
                    title Demo Runner
                    echo.
                    echo   SGSClient seed game — Demo Runner
                    echo   Closing in 3 seconds...
                    echo.
                    timeout /t 3 /nobreak >nul
                    """;
                WriteEntry(zip, executable, Encoding.ASCII.GetBytes(cmd.Replace("\r\n", "\n").Replace("\n", "\r\n")));
            }
            else
            {
                var sh = """
                    #!/bin/sh
                    echo ""
                    echo "  SGSClient seed game — Demo Runner"
                    echo "  Closing in 3 seconds..."
                    echo ""
                    sleep 3
                    """;
                WriteEntry(zip, executable, Encoding.UTF8.GetBytes(sh.Replace("\r\n", "\n")));
            }

            WriteEntry(zip, "data/readme.txt", Encoding.UTF8.GetBytes(
                "Seed package generated by SGSClient API. Safe to reinstall.\n"));
        }

        File.Move(temp, zipPath, overwrite: true);
    }

    private static void WriteEntry(ZipArchive zip, string entryName, byte[] bytes)
    {
        var entry = zip.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes);
    }
}
