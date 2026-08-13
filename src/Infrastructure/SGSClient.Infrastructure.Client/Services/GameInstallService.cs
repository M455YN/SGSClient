using System.IO.Compression;
using System.Net;
using Microsoft.Extensions.Options;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;
using SGSClient.Core.Models;

namespace SGSClient.Infrastructure.Client.Services;

public sealed class GameInstallService(
    HttpClient http,
    ISgsApiService api,
    IOptions<ApiClientSettings> apiOptions,
    ILibraryPathProvider libraryPaths) : IGameInstallService
{
    private readonly string _apiBaseUrl = apiOptions.Value.BaseUrl;

    public string ResolveDownloadUrl(GameBuildDto build) =>
        InstallPathHelper.ResolveDownloadUrl(_apiBaseUrl, build.ZipLink);

    public async Task<GameInstallResult> InstallAsync(
        GameLibraryItem item,
        string? version = null,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(InstallProgress.Of(InstallPhase.Queued, 0, "Przygotowanie instalacji…"));

        var game = await api.GetGameAsync(item.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return FailItem(item, InstallState.Failed, "Nie znaleziono gry w sklepie.");
        }

        var build = game.Builds.FirstOrDefault(b => b.Id == item.BuildId)
            ?? game.Builds.FirstOrDefault(b => b.Platform == item.Platform
                && string.Equals(b.Architecture ?? "x64", item.Architecture ?? "x64", StringComparison.OrdinalIgnoreCase))
            ?? game.Builds.FirstOrDefault(b => b.IsDefault)
            ?? game.Builds.FirstOrDefault();

        if (build is null)
        {
            return FailItem(item, InstallState.Failed, "Brak builda do pobrania dla tej gry.");
        }

        var targetVersion = string.IsNullOrWhiteSpace(version) ? build.CurrentVersion : version.Trim();
        var exeName = build.ExeName;
        var downloadUrl = ResolveDownloadUrl(build);

        if (!string.Equals(targetVersion, build.CurrentVersion, StringComparison.OrdinalIgnoreCase))
        {
            var history = await api.GetBuildVersionsAsync(item.GameId, build.Id, cancellationToken)
                .ConfigureAwait(false);
            var selected = history.FirstOrDefault(v =>
                string.Equals(v.Version, targetVersion, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                return FailItem(item, InstallState.Failed, $"Wersja {targetVersion} nie jest dostępna na serwerze.");
            }

            exeName = selected.ExeName;
            downloadUrl = InstallPathHelper.ResolveDownloadUrl(_apiBaseUrl, selected.ZipLink);
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return FailItem(item, InstallState.Failed, "Brak adresu pobierania paczki (ZipLink).");
        }

        item.InstallState = InstallState.Downloading;
        progress?.Report(InstallProgress.Of(InstallPhase.Downloading, 0, "Pobieranie paczki…"));

        var libraryRoot = libraryPaths.GetPrimaryLibraryFolder();
        var tempZip = Path.Combine(Path.GetTempPath(), $"sgs-dl-{item.Symbol}-{targetVersion}-{Guid.NewGuid():N}.zip");
        var installDir = InstallPathHelper.GetInstallDirectory(libraryRoot, item.Symbol, targetVersion);

        try
        {
            Directory.CreateDirectory(SgsLibraryLayout.GetCommonRoot(libraryRoot));

            await DownloadAsync(downloadUrl, tempZip, progress, cancellationToken).ConfigureAwait(false);

            item.InstallState = InstallState.Installing;
            progress?.Report(InstallProgress.Of(InstallPhase.Installing, 0.9, "Rozpakowywanie…"));

            if (Directory.Exists(installDir))
            {
                Directory.Delete(installDir, recursive: true);
            }

            Directory.CreateDirectory(installDir);
            ZipFile.ExtractToDirectory(tempZip, installDir, overwriteFiles: true);

            var executablePath = InstallPathHelper.CombineExecutablePath(installDir, exeName);
            if (!File.Exists(executablePath))
            {
                return FailItem(item, InstallState.Failed,
                    $"Po rozpakowaniu brak pliku uruchomieniowego: {exeName}");
            }

            item.InstallState = InstallState.Installed;
            item.InstallPath = installDir;
            item.ExecutablePath = executablePath;
            item.InstalledVersion = targetVersion;
            item.Title = game.Title;
            item.Platform = build.Platform;
            item.Architecture = build.Architecture;

            await SgsInstallRecordWriter.WriteAsync(item, libraryRoot, exeName, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(InstallProgress.Of(InstallPhase.Completed, 1, "Zainstalowano."));
            return GameInstallResult.Ok(item);
        }
        catch (OperationCanceledException)
        {
            item.InstallState = InstallState.NotInstalled;
            throw;
        }
        catch (Exception ex)
        {
            return FailItem(item, InstallState.Failed, $"Instalacja nie powiodła się: {ex.Message}");
        }
        finally
        {
            TryDelete(tempZip);
        }
    }

    public Task<GameInstallResult> UninstallAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (item.InstallState != InstallState.Installed)
        {
            return Task.FromResult(GameInstallResult.Fail("Gra nie jest zainstalowana."));
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(item.InstallPath) && Directory.Exists(item.InstallPath))
            {
                Directory.Delete(item.InstallPath, recursive: true);
            }

            foreach (var libraryRoot in libraryPaths.GetLibraryFolders())
            {
                TryDeleteVersionFolderIfEmpty(libraryRoot, item.Symbol, item.InstalledVersion);
                SgsInstallRecordWriter.TryDeleteCentralManifest(libraryRoot, item.GameId, item.BuildId);
            }

            item.InstallState = InstallState.NotInstalled;
            item.InstallPath = null;
            item.ExecutablePath = null;
            item.InstalledVersion = null;

            return Task.FromResult(GameInstallResult.Ok(item));
        }
        catch (Exception ex)
        {
            return Task.FromResult(GameInstallResult.Fail($"Odinstalowanie nie powiodło się: {ex.Message}"));
        }
    }

    public async Task<GameInstallResult> MoveAsync(
        GameLibraryItem item,
        string destinationLibraryRoot,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (item.InstallState != InstallState.Installed)
        {
            return GameInstallResult.Fail("Gra nie jest zainstalowana.");
        }

        if (string.IsNullOrWhiteSpace(item.InstallPath) || !Directory.Exists(item.InstallPath))
        {
            return GameInstallResult.Fail("Folder instalacji nie istnieje.");
        }

        if (string.IsNullOrWhiteSpace(item.InstalledVersion))
        {
            return GameInstallResult.Fail("Brak informacji o zainstalowanej wersji.");
        }

        if (string.IsNullOrWhiteSpace(destinationLibraryRoot))
        {
            return GameInstallResult.Fail("Nie wybrano folderu docelowego.");
        }

        var sourcePath = Path.GetFullPath(item.InstallPath);
        var destinationRoot = Path.GetFullPath(destinationLibraryRoot);
        var knownRoots = libraryPaths.GetLibraryFolders()
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!knownRoots.Any(r => string.Equals(r, destinationRoot, StringComparison.OrdinalIgnoreCase)))
        {
            return GameInstallResult.Fail("Folder docelowy nie jest zarejestrowanym folderem biblioteki.");
        }

        var sourceRoot = FindLibraryRoot(sourcePath, knownRoots);
        if (sourceRoot is not null
            && string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            return GameInstallResult.Fail("Gra jest już w wybranym folderze biblioteki.");
        }

        var destinationPath = Path.GetFullPath(
            InstallPathHelper.GetInstallDirectory(destinationRoot, item.Symbol, item.InstalledVersion));

        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return GameInstallResult.Fail("Ścieżka źródłowa i docelowa są takie same.");
        }

        if (Directory.Exists(destinationPath)
            && Directory.EnumerateFileSystemEntries(destinationPath).Any())
        {
            return GameInstallResult.Fail("W folderze docelowym już istnieją pliki tej gry.");
        }

        progress?.Report(InstallProgress.Of(InstallPhase.Installing, 0.1, "Przenoszenie plików…"));

        try
        {
            Directory.CreateDirectory(SgsLibraryLayout.GetCommonRoot(destinationRoot));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath, recursive: true);
            }

            await Task.Run(() => MoveDirectory(sourcePath, destinationPath), cancellationToken)
                .ConfigureAwait(false);

            var relativeExe = string.IsNullOrWhiteSpace(item.ExecutablePath)
                ? null
                : Path.GetRelativePath(sourcePath, item.ExecutablePath);

            item.InstallPath = destinationPath;
            if (!string.IsNullOrWhiteSpace(relativeExe) && relativeExe is not ".")
            {
                item.ExecutablePath = Path.GetFullPath(Path.Combine(destinationPath, relativeExe));
            }
            else if (!string.IsNullOrWhiteSpace(item.ExecutablePath))
            {
                item.ExecutablePath = Path.Combine(
                    destinationPath,
                    Path.GetFileName(item.ExecutablePath));
            }

            if (sourceRoot is not null)
            {
                SgsInstallRecordWriter.TryDeleteCentralManifest(sourceRoot, item.GameId, item.BuildId);
                TryDeleteVersionFolderIfEmpty(sourceRoot, item.Symbol, item.InstalledVersion);
            }

            var exeRelative = string.IsNullOrWhiteSpace(relativeExe) || relativeExe == "."
                ? Path.GetFileName(item.ExecutablePath) ?? "game.exe"
                : relativeExe.Replace('\\', '/');

            await SgsInstallRecordWriter.WriteAsync(item, destinationRoot, exeRelative, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(InstallProgress.Of(InstallPhase.Completed, 1, "Przeniesiono."));
            return GameInstallResult.Ok(item);
        }
        catch (Exception ex)
        {
            return GameInstallResult.Fail($"Przenoszenie nie powiodło się: {ex.Message}");
        }
    }

    private static string? FindLibraryRoot(string installPath, IReadOnlyList<string> libraryRoots)
    {
        var fullInstall = Path.GetFullPath(installPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        foreach (var root in libraryRoots.OrderByDescending(r => r.Length))
        {
            var fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (fullInstall.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullInstall.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return fullRoot;
            }
        }

        return null;
    }

    private static void MoveDirectory(string source, string destination)
    {
        try
        {
            Directory.Move(source, destination);
            return;
        }
        catch (IOException)
        {
            // Cross-volume move falls back to copy + delete.
        }

        CopyDirectory(source, destination);
        Directory.Delete(source, recursive: true);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, target, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(directory));
            CopyDirectory(directory, target);
        }
    }

    private async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Paczka nie została znaleziona na serwerze (404).");
        }

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = File.Create(destinationPath);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await network.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            readTotal += read;

            if (total is > 0)
            {
                var fraction = readTotal / (double)total.Value * 0.85;
                progress?.Report(InstallProgress.Of(InstallPhase.Downloading, fraction, "Pobieranie paczki…"));
            }
        }
    }

    private static void TryDeleteVersionFolderIfEmpty(string libraryRoot, string symbol, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        foreach (var candidate in InstallPathHelper.GetInstallDirectoryCandidates(libraryRoot, symbol, version))
        {
            if (Directory.Exists(candidate) && !Directory.EnumerateFileSystemEntries(candidate).Any())
            {
                Directory.Delete(candidate, recursive: false);
            }
        }

        foreach (var symbolDir in new[]
                 {
                     Path.Combine(SgsLibraryLayout.GetCommonRoot(libraryRoot), symbol),
                     Path.Combine(libraryRoot, symbol)
                 })
        {
            if (Directory.Exists(symbolDir) && !Directory.EnumerateFileSystemEntries(symbolDir).Any())
            {
                Directory.Delete(symbolDir, recursive: false);
            }
        }
    }

    private static GameInstallResult FailItem(GameLibraryItem item, InstallState state, string error)
    {
        item.InstallState = state;
        return GameInstallResult.Fail(error);
    }

    private static void TryDelete(string path)
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
            // ignore temp cleanup
        }
    }
}
