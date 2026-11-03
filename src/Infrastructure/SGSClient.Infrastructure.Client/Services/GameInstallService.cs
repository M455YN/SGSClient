using SGSClient.Core.Localization;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
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
    IAuthSession? auth,
    IOptions<ApiClientSettings> apiOptions,
    ILibraryPathProvider libraryPaths,
    ILocalLibraryStore library,
    IClientUserSettingsStore? userSettings = null) : IGameInstallService
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
        progress?.Report(InstallProgress.Of(InstallPhase.Queued, 0, Loc.T("Przygotowanie instalacji…")));

        var game = await api.GetGameAsync(item.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return FailItem(item, InstallState.Failed, Loc.T("Nie znaleziono gry w sklepie."));
        }

        if (game.Kind == GameContentKind.Dlc)
        {
            if (game.ParentGameId is not int parentId || parentId <= 0)
            {
                return FailItem(item, InstallState.Failed, Loc.T("DLC nie ma powiązanej gry bazowej."));
            }

            var parent = await library.GetByGameIdAsync(parentId, cancellationToken).ConfigureAwait(false);
            var parentInstalled = parent is not null
                && parent.InstallState == InstallState.Installed
                && !string.IsNullOrWhiteSpace(parent.InstallPath)
                && Directory.Exists(parent.InstallPath);
            if (!parentInstalled)
            {
                return FailItem(
                    item,
                    InstallState.Failed,
                    Loc.T("Najpierw zainstaluj grę bazową, zanim zainstalujesz DLC."));
            }
        }

        var build = game.Builds.FirstOrDefault(b => b.Id == item.BuildId)
            ?? game.Builds.FirstOrDefault(b => b.Platform == item.Platform
                && string.Equals(b.Architecture ?? "x64", item.Architecture ?? "x64", StringComparison.OrdinalIgnoreCase))
            ?? game.Builds.FirstOrDefault(b => b.IsDefault)
            ?? game.Builds.FirstOrDefault();

        if (build is null)
        {
            return FailItem(item, InstallState.Failed, Loc.T("Brak builda do pobrania dla tej gry."));
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
                return FailItem(item, InstallState.Failed, Loc.F("Wersja {0} nie jest dostępna na serwerze.", targetVersion));
            }

            exeName = selected.ExeName;
            downloadUrl = InstallPathHelper.ResolveDownloadUrl(_apiBaseUrl, selected.ZipLink);
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return FailItem(item, InstallState.Failed, Loc.T("Brak adresu pobierania paczki (ZipLink)."));
        }

        item.InstallState = InstallState.Downloading;
        progress?.Report(InstallProgress.Of(InstallPhase.Downloading, 0, Loc.T("Pobieranie paczki…")));

        var libraryRoot = libraryPaths.GetPrimaryLibraryFolder();
        var tempZip = Path.Combine(Path.GetTempPath(), $"sgs-dl-{item.Symbol}-{targetVersion}-{Guid.NewGuid():N}.zip");
        var installDir = InstallPathHelper.GetInstallDirectory(libraryRoot, item.Symbol, targetVersion);

        try
        {
            Directory.CreateDirectory(SgsLibraryLayout.GetCommonRoot(libraryRoot));

            await DownloadAsync(downloadUrl, tempZip, progress, cancellationToken).ConfigureAwait(false);

            item.InstallState = InstallState.Installing;
            progress?.Report(InstallProgress.Of(InstallPhase.Installing, 0.9, Loc.T("Rozpakowywanie…")));

            if (Directory.Exists(installDir))
            {
                Directory.Delete(installDir, recursive: true);
            }

            Directory.CreateDirectory(installDir);
            ZipFile.ExtractToDirectory(tempZip, installDir, overwriteFiles: true);

            TryMarkUnixExecutable(InstallPathHelper.CombineExecutablePath(installDir, exeName));

            var executablePath = InstallPathHelper.CombineExecutablePath(installDir, exeName);
            if (!File.Exists(executablePath))
            {
                return FailItem(item, InstallState.Failed,
                    Loc.F("Po rozpakowaniu brak pliku uruchomieniowego: {0}", exeName));
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

            DesktopShortcutHelper.CreateForGame(item.Title, executablePath, installDir);

            progress?.Report(InstallProgress.Of(InstallPhase.Completed, 1, Loc.T("Zainstalowano.")));
            return GameInstallResult.Ok(item);
        }
        catch (OperationCanceledException)
        {
            item.InstallState = InstallState.NotInstalled;
            throw;
        }
        catch (Exception ex)
        {
            return FailItem(item, InstallState.Failed, Loc.F("Instalacja nie powiodła się: {0}", ex.Message));
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
            return Task.FromResult(GameInstallResult.Fail(Loc.T("Gra nie jest zainstalowana.")));
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

            DesktopShortcutHelper.RemoveForGame(item.Title);

            item.InstallState = InstallState.NotInstalled;
            item.InstallPath = null;
            item.ExecutablePath = null;
            item.InstalledVersion = null;

            return Task.FromResult(GameInstallResult.Ok(item));
        }
        catch (Exception ex)
        {
            return Task.FromResult(GameInstallResult.Fail(Loc.F("Odinstalowanie nie powiodło się: {0}", ex.Message)));
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
            return GameInstallResult.Fail(Loc.T("Gra nie jest zainstalowana."));
        }

        if (string.IsNullOrWhiteSpace(item.InstallPath) || !Directory.Exists(item.InstallPath))
        {
            return GameInstallResult.Fail(Loc.T("Folder instalacji nie istnieje."));
        }

        if (string.IsNullOrWhiteSpace(item.InstalledVersion))
        {
            return GameInstallResult.Fail(Loc.T("Brak informacji o zainstalowanej wersji."));
        }

        if (string.IsNullOrWhiteSpace(destinationLibraryRoot))
        {
            return GameInstallResult.Fail(Loc.T("Nie wybrano folderu docelowego."));
        }

        var sourcePath = Path.GetFullPath(item.InstallPath);
        var destinationRoot = Path.GetFullPath(destinationLibraryRoot);
        var knownRoots = libraryPaths.GetLibraryFolders()
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!knownRoots.Any(r => string.Equals(r, destinationRoot, StringComparison.OrdinalIgnoreCase)))
        {
            return GameInstallResult.Fail(Loc.T("Folder docelowy nie jest zarejestrowanym folderem biblioteki."));
        }

        var sourceRoot = FindLibraryRoot(sourcePath, knownRoots);
        if (sourceRoot is not null
            && string.Equals(sourceRoot, destinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            return GameInstallResult.Fail(Loc.T("Gra jest już w wybranym folderze biblioteki."));
        }

        var destinationPath = Path.GetFullPath(
            InstallPathHelper.GetInstallDirectory(destinationRoot, item.Symbol, item.InstalledVersion));

        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return GameInstallResult.Fail(Loc.T("Ścieżka źródłowa i docelowa są takie same."));
        }

        if (Directory.Exists(destinationPath)
            && Directory.EnumerateFileSystemEntries(destinationPath).Any())
        {
            return GameInstallResult.Fail(Loc.T("W folderze docelowym już istnieją pliki tej gry."));
        }

        progress?.Report(InstallProgress.Of(InstallPhase.Installing, 0.1, Loc.T("Przenoszenie plików…")));

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
            return GameInstallResult.Fail(Loc.F("Przenoszenie nie powiodło się: {0}", ex.Message));
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
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (auth is { IsAvailable: true, IsLoggedIn: true })
        {
            var token = await auth.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException(Loc.T("Zaloguj się, żeby pobrać grę (401)."));
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                Loc.T("Brak uprawnień do pobrania. Dodaj grę do biblioteki (403)."));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(Loc.T("Paczka nie została znaleziona na serwerze (404)."));
        }

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = File.Create(destinationPath);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        var maxKbps = 0;
        try
        {
            maxKbps = Math.Max(0, userSettings?.Load().DownloadMaxKbps ?? 0);
        }
        catch
        {
            maxKbps = 0;
        }

        var windowStart = Stopwatch.GetTimestamp();
        long windowBytes = 0;
        var lastProgressReport = Stopwatch.GetTimestamp();
        double lastReportedFraction = -1;
        while ((read = await network.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            readTotal += read;
            windowBytes += read;

            if (maxKbps > 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(windowStart);
                var allowedBytes = maxKbps * 1024.0 * Math.Max(elapsed.TotalSeconds, 0.001);
                if (windowBytes > allowedBytes)
                {
                    var delayMs = (int)Math.Ceiling((windowBytes - allowedBytes) / (maxKbps * 1024.0) * 1000.0);
                    if (delayMs > 0)
                    {
                        await Task.Delay(Math.Min(delayMs, 2000), cancellationToken).ConfigureAwait(false);
                    }
                }

                if (elapsed.TotalSeconds >= 1.0)
                {
                    windowStart = Stopwatch.GetTimestamp();
                    windowBytes = 0;
                }
            }

            if (total is > 0 && progress is not null)
            {
                var fraction = readTotal / (double)total.Value * 0.85;
                // Avoid flooding the UI thread — report ~10×/s or when progress jumps ≥0.5%.
                var sinceLast = Stopwatch.GetElapsedTime(lastProgressReport);
                if (sinceLast.TotalMilliseconds >= 100
                    || fraction - lastReportedFraction >= 0.005
                    || readTotal >= total.Value)
                {
                    lastProgressReport = Stopwatch.GetTimestamp();
                    lastReportedFraction = fraction;
                    progress.Report(InstallProgress.Of(InstallPhase.Downloading, fraction, Loc.T("Pobieranie paczki…")));
                }
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

    private static void TryMarkUnixExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            // Best-effort: extracted scripts from ZIP are often non-executable.
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                ArgumentList = { "+x", path },
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(2000);
        }
        catch
        {
            // ignore
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
