using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.ViewModels;

public partial class PublishViewModel : ViewModelBase
{
    private readonly IGamePackagePacker? _packer;
    private readonly IGameUploadClient? _upload;
    private readonly IAuthSession? _auth;
    private readonly ISgsApiService? _api;
    private string? _packedZipPath;
    private SgsPackageHints? _packageHints;

    public PublishViewModel()
    {
        Title = "Publikuj grę";
        StatusText = "Wybierz folder z grą (musi zawierać sgs-manifest.json).";
    }

    public PublishViewModel(
        IGamePackagePacker? packer,
        IGameUploadClient? upload,
        IAuthSession? auth,
        ISgsApiService? api = null)
    {
        _packer = packer;
        _upload = upload;
        _auth = auth;
        _api = api;
        Title = "Publikuj grę";
        StatusText = "Wybierz folder z grą (musi zawierać sgs-manifest.json).";
        RefreshAuthHint();
    }

    public string Title { get; }

    public ObservableCollection<string> Issues { get; } = [];

    public ObservableCollection<string> ParentGameOptions { get; } = [];

    public IReadOnlyList<string> PlatformOptions { get; } = ["windows", "linux", "macos"];

    public IReadOnlyList<string> ArchitectureOptions { get; } = ["x64", "x86", "arm64"];

    public IReadOnlyList<string> GenreOptions { get; } =
        SgsGameTaxonomy.Genres.Select(g => g.Symbol).Prepend("").ToArray();

    public IReadOnlyList<string> EngineOptions { get; } =
        SgsGameTaxonomy.Engines.Select(e => e.Symbol).Prepend("").ToArray();

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _selectedFolder = string.Empty;

    [ObservableProperty]
    private bool _hasManifestForm;

    [ObservableProperty]
    private bool _isDlc;

    [ObservableProperty]
    private string _parentSymbol = string.Empty;

    [ObservableProperty]
    private string _gameTitle = string.Empty;

    [ObservableProperty]
    private string _gameSymbol = string.Empty;

    [ObservableProperty]
    private string _gameDescription = string.Empty;

    [ObservableProperty]
    private string _hardwareRequirements = string.Empty;

    [ObservableProperty]
    private string _otherInformation = string.Empty;

    [ObservableProperty]
    private string _gameGenre = string.Empty;

    [ObservableProperty]
    private string _gameEngine = string.Empty;

    [ObservableProperty]
    private string _mediaCover = string.Empty;

    [ObservableProperty]
    private string _mediaHeader = string.Empty;

    [ObservableProperty]
    private string _mediaIntro = string.Empty;

    [ObservableProperty]
    private string _mediaScreenshots = string.Empty;

    [ObservableProperty]
    private string _buildVersion = string.Empty;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    [ObservableProperty]
    private string _buildPlatform = "windows";

    [ObservableProperty]
    private string _buildArchitecture = "x64";

    [ObservableProperty]
    private string _buildExecutable = string.Empty;

    [ObservableProperty]
    private string _summaryTitle = string.Empty;

    [ObservableProperty]
    private string _summaryMeta = string.Empty;

    [ObservableProperty]
    private bool _hasSummary;

    [ObservableProperty]
    private bool _canPublish;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _showProgress;

    [ObservableProperty]
    private string _authHint = string.Empty;

    public bool ShowParentField => IsDlc;

    partial void OnIsDlcChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowParentField));
        if (value)
        {
            _ = LoadParentOptionsAsync();
        }
    }

    public async Task SetFolderAsync(string folder)
    {
        SelectedFolder = folder;
        HasSummary = false;
        CanPublish = false;
        HasManifestForm = false;
        Issues.Clear();
        CleanupPackedZip();
        var keepDlc = IsDlc;
        var keepParent = ParentSymbol;
        ClearForm();
        IsDlc = keepDlc;
        ParentSymbol = keepParent;
        _packageHints = null;

        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusText = "Wybierz folder z grą.";
            return;
        }

        StatusText = $"Wybrano: {folder}";
        _ = LoadParentOptionsAsync();
        if (_packer is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var loaded = await _packer.LoadManifestAsync(folder);
            if (!loaded.Success || loaded.Manifest is null)
            {
                foreach (var error in loaded.Errors)
                {
                    Issues.Add(error);
                }

                StatusText = "Nie udało się wczytać manifestu z folderu.";
                return;
            }

            ApplyManifestToForm(loaded.Manifest, preferExistingDlc: keepDlc);
            HasManifestForm = true;
            StatusText = "Uzupełnij lub popraw dane gry, potem przygotuj paczkę.";
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd odczytu manifestu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshAuthHint()
    {
        if (_auth?.IsAvailable != true)
        {
            AuthHint = "Logowanie jest wyłączone — lokalnie upload pójdzie bez konta.";
        }
        else
        {
            AuthHint = _auth.IsLoggedIn
                ? $"Zalogowano jako {_auth.CurrentUser?.DisplayName ?? "gracz"}."
                : "Zaloguj się w Profilu przed publikacją (wymagane na serwerze z B2C).";
        }

        _ = LoadParentOptionsAsync();
    }

    public void PrepareForDlc(string parentSymbol)
    {
        IsDlc = true;
        ParentSymbol = parentSymbol?.Trim() ?? string.Empty;
        StatusText = string.IsNullOrWhiteSpace(ParentSymbol)
            ? "Tryb DLC — wybierz grę bazową i folder z paczką."
            : $"Tryb DLC dla gry bazowej: {ParentSymbol}. Wybierz folder z paczką.";
        _ = LoadParentOptionsAsync();
    }

    public void PrepareForNewGame()
    {
        IsDlc = false;
        ParentSymbol = string.Empty;
        StatusText = "Wybierz folder z grą (musi zawierać sgs-manifest.json).";
    }

    public async Task LoadParentOptionsAsync()
    {
        if (_api is null)
        {
            return;
        }

        try
        {
            var games = await _api.GetMyGamesAsync();
            var symbols = games
                .Where(g => g.Kind == GameContentKind.Game)
                .Select(g => g.Symbol)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ParentGameOptions.Clear();
            foreach (var symbol in symbols)
            {
                ParentGameOptions.Add(symbol);
            }
        }
        catch
        {
            // best-effort
        }
    }

    [RelayCommand]
    private async Task PrepareAsync()
    {
        if (_packer is null || string.IsNullOrWhiteSpace(SelectedFolder) || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ShowProgress = false;
        Issues.Clear();
        CanPublish = false;
        HasSummary = false;
        CleanupPackedZip();
        StatusText = "Zapisuję dane i pakuję grę…";

        try
        {
            var manifest = BuildManifestFromForm();
            var result = await _packer.PackFolderAsync(SelectedFolder, manifest);
            if (!result.Success || result.Manifest is null || string.IsNullOrWhiteSpace(result.ZipPath))
            {
                foreach (var error in result.Errors)
                {
                    Issues.Add(error);
                }

                StatusText = "Nie udało się przygotować paczki.";
                return;
            }

            _packedZipPath = result.ZipPath;
            var m = result.Manifest;
            _packageHints = m.Package;
            SummaryTitle = m.Game.Title;
            SummaryMeta =
                $"{m.Game.Symbol} · {m.Build.Platform}/{m.Build.Architecture ?? "x64"} · v{m.Build.Version} · {m.Build.Executable}";
            HasSummary = true;
            CanPublish = true;
            StatusText = "Paczka gotowa (manifest zapisany w folderze). Możesz ją wysłać do sklepu.";
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd pakowania: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PublishAsync()
    {
        if (_upload is null || string.IsNullOrWhiteSpace(_packedZipPath) || IsBusy)
        {
            return;
        }

        RefreshAuthHint();
        IsBusy = true;
        ShowProgress = true;
        Progress = 0;
        Issues.Clear();
        StatusText = "Wysyłanie do sklepu…";

        try
        {
            var progress = new Progress<double>(p => Progress = p);
            var result = await _upload.UploadAsync(_packedZipPath, progress);
            if (!result.Success || result.Game is null)
            {
                foreach (var error in result.Errors)
                {
                    Issues.Add(error);
                }

                StatusText = "Publikacja nie powiodła się.";
                return;
            }

            StatusText = $"Opublikowano: {result.Game.Title}. Znajdziesz ją w Sklepie.";
            CanPublish = false;
            CleanupPackedZip();
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd wysyłania: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ShowProgress = false;
        }
    }

    public bool TrySetCoverFromFile(string sourcePath, out string? error)
    {
        if (!TryImportMediaFile(sourcePath, SgsPackageRules.AllowedImageExtensions, "cover", out var relative, out error))
        {
            return false;
        }

        MediaCover = relative;
        StatusText = $"Ustawiono miniaturkę: {relative}";
        return true;
    }

    public bool TrySetHeaderFromFile(string sourcePath, out string? error)
    {
        if (!TryImportMediaFile(sourcePath, SgsPackageRules.AllowedImageExtensions, "header", out var relative, out error))
        {
            return false;
        }

        MediaHeader = relative;
        StatusText = $"Ustawiono okładkę poziomą: {relative}";
        return true;
    }

    public bool TrySetIntroFromFile(string sourcePath, out string? error)
    {
        if (!TryImportMediaFile(sourcePath, SgsPackageRules.AllowedVideoExtensions, "intro", out var relative, out error))
        {
            return false;
        }

        MediaIntro = relative;
        StatusText = $"Ustawiono intro: {relative}";
        return true;
    }

    public bool TryAddScreenshotsFromFiles(IEnumerable<string> sourcePaths, out string? error)
    {
        error = null;
        var added = new List<string>();
        foreach (var sourcePath in sourcePaths)
        {
            if (!TryImportMediaFile(sourcePath, SgsPackageRules.AllowedImageExtensions, preferredStem: null, out var relative, out error))
            {
                return false;
            }

            added.Add(relative);
        }

        if (added.Count == 0)
        {
            error = "Nie wybrano plików.";
            return false;
        }

        var existing = MediaScreenshots
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        foreach (var path in added)
        {
            if (!existing.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                existing.Add(path);
            }
        }

        if (existing.Count > SgsPackageRules.MaxScreenshots)
        {
            error = $"Maksymalnie {SgsPackageRules.MaxScreenshots} screenów.";
            return false;
        }

        MediaScreenshots = string.Join(Environment.NewLine, existing);
        StatusText = $"Dodano screeny ({added.Count}). Łącznie: {existing.Count}.";
        return true;
    }

    public void ClearCover()
    {
        MediaCover = string.Empty;
        StatusText = "Usunięto miniaturkę z formularza.";
    }

    public void ClearHeader()
    {
        MediaHeader = string.Empty;
        StatusText = "Usunięto okładkę poziomą z formularza.";
    }

    public void ClearIntro()
    {
        MediaIntro = string.Empty;
        StatusText = "Usunięto intro z formularza.";
    }

    public void ClearScreenshots()
    {
        MediaScreenshots = string.Empty;
        StatusText = "Usunięto listę screenów z formularza.";
    }

    private bool TryImportMediaFile(
        string sourcePath,
        HashSet<string> allowedExtensions,
        string? preferredStem,
        out string relativePath,
        out string? error)
    {
        relativePath = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(SelectedFolder) || !Directory.Exists(SelectedFolder))
        {
            error = "Najpierw wybierz folder gry.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            error = "Nie znaleziono wybranego pliku.";
            return false;
        }

        var ext = Path.GetExtension(sourcePath);
        if (!allowedExtensions.Contains(ext))
        {
            error = $"Niedozwolone rozszerzenie '{ext}'. Dozwolone: {string.Join(", ", allowedExtensions)}.";
            return false;
        }

        var root = Path.GetFullPath(SelectedFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullSource = Path.GetFullPath(sourcePath);

        if (fullSource.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullSource, root, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = Path.GetRelativePath(root, fullSource).Replace('\\', '/');
            return true;
        }

        var mediaDir = Path.Combine(root, "media");
        Directory.CreateDirectory(mediaDir);

        var fileName = string.IsNullOrWhiteSpace(preferredStem)
            ? Path.GetFileName(fullSource)
            : preferredStem + ext.ToLowerInvariant();

        var dest = Path.Combine(mediaDir, fileName);
        if (File.Exists(dest)
            && !string.Equals(Path.GetFullPath(dest), fullSource, StringComparison.OrdinalIgnoreCase))
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            fileName = $"{stem}-{DateTime.Now:HHmmss}{ext.ToLowerInvariant()}";
            dest = Path.Combine(mediaDir, fileName);
        }

        try
        {
            File.Copy(fullSource, dest, overwrite: true);
        }
        catch (Exception ex)
        {
            error = $"Nie udało się skopiować pliku do folderu gry: {ex.Message}";
            return false;
        }

        relativePath = Path.Combine("media", fileName).Replace('\\', '/');
        return true;
    }

    private void ApplyManifestToForm(SgsPackageManifest manifest, bool preferExistingDlc = false)
    {
        var rawKind = manifest.Game.Kind;
        var kind = SgsPackageRules.NormalizeContentKind(rawKind);
        var manifestIsDlc = string.Equals(kind, "dlc", StringComparison.OrdinalIgnoreCase);
        if (manifestIsDlc)
        {
            IsDlc = true;
            ParentSymbol = string.IsNullOrWhiteSpace(manifest.Game.ParentSymbol)
                ? ParentSymbol
                : manifest.Game.ParentSymbol!;
        }
        else if (!preferExistingDlc || !string.IsNullOrWhiteSpace(rawKind))
        {
            IsDlc = false;
            ParentSymbol = string.Empty;
        }

        GameTitle = manifest.Game.Title ?? string.Empty;
        GameSymbol = manifest.Game.Symbol ?? string.Empty;
        GameDescription = manifest.Game.Description ?? string.Empty;
        HardwareRequirements = manifest.Game.HardwareRequirements ?? string.Empty;
        OtherInformation = manifest.Game.OtherInformation ?? string.Empty;
        GameGenre = manifest.Game.Genre ?? string.Empty;
        GameEngine = manifest.Game.Engine ?? string.Empty;
        MediaCover = manifest.Game.Media?.Cover ?? string.Empty;
        MediaHeader = manifest.Game.Media?.Header ?? string.Empty;
        MediaIntro = manifest.Game.Media?.Intro ?? string.Empty;
        MediaScreenshots = manifest.Game.Media?.Screenshots is { Count: > 0 } shots
            ? string.Join(Environment.NewLine, shots)
            : string.Empty;
        BuildVersion = manifest.Build.Version ?? string.Empty;
        ReleaseNotes = manifest.Build.ReleaseNotes ?? string.Empty;
        BuildPlatform = string.IsNullOrWhiteSpace(manifest.Build.Platform) ? "windows" : manifest.Build.Platform;
        BuildArchitecture = string.IsNullOrWhiteSpace(manifest.Build.Architecture) ? "x64" : manifest.Build.Architecture;
        BuildExecutable = manifest.Build.Executable ?? string.Empty;
        _packageHints = manifest.Package;
    }

    private SgsPackageManifest BuildManifestFromForm()
    {
        var screenshots = MediaScreenshots
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .ToList();

        SgsPackageMediaInfo? media = null;
        if (!string.IsNullOrWhiteSpace(MediaCover)
            || !string.IsNullOrWhiteSpace(MediaHeader)
            || !string.IsNullOrWhiteSpace(MediaIntro)
            || screenshots.Count > 0)
        {
            media = new SgsPackageMediaInfo
            {
                Cover = NullIfWhiteSpace(MediaCover),
                Header = NullIfWhiteSpace(MediaHeader),
                Intro = NullIfWhiteSpace(MediaIntro),
                Screenshots = screenshots
            };
        }

        return new SgsPackageManifest
        {
            SchemaVersion = SgsPackageManifest.CurrentSchemaVersion,
            Game = new SgsPackageGameInfo
            {
                Title = GameTitle.Trim(),
                Symbol = GameSymbol.Trim(),
                Kind = IsDlc ? "dlc" : "game",
                ParentSymbol = IsDlc ? NullIfWhiteSpace(ParentSymbol) : null,
                Description = NullIfWhiteSpace(GameDescription),
                HardwareRequirements = NullIfWhiteSpace(HardwareRequirements),
                OtherInformation = NullIfWhiteSpace(OtherInformation),
                Genre = NullIfWhiteSpace(GameGenre),
                Engine = NullIfWhiteSpace(GameEngine),
                Media = media
            },
            Build = new SgsPackageBuildInfo
            {
                Version = BuildVersion.Trim(),
                Platform = (BuildPlatform ?? "windows").Trim().ToLowerInvariant(),
                Architecture = string.IsNullOrWhiteSpace(BuildArchitecture) ? "x64" : BuildArchitecture.Trim(),
                Executable = BuildExecutable.Trim().Replace('\\', '/'),
                IsDefault = true,
                ReleaseNotes = NullIfWhiteSpace(ReleaseNotes)
            },
            Package = _packageHints
        };
    }

    private void ClearForm()
    {
        IsDlc = false;
        ParentSymbol = string.Empty;
        GameTitle = string.Empty;
        GameSymbol = string.Empty;
        GameDescription = string.Empty;
        HardwareRequirements = string.Empty;
        OtherInformation = string.Empty;
        GameGenre = string.Empty;
        GameEngine = string.Empty;
        MediaCover = string.Empty;
        MediaHeader = string.Empty;
        MediaIntro = string.Empty;
        MediaScreenshots = string.Empty;
        BuildVersion = string.Empty;
        ReleaseNotes = string.Empty;
        BuildPlatform = "windows";
        BuildArchitecture = "x64";
        BuildExecutable = string.Empty;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void CleanupPackedZip()
    {
        if (string.IsNullOrWhiteSpace(_packedZipPath))
        {
            return;
        }

        try
        {
            if (File.Exists(_packedZipPath))
            {
                File.Delete(_packedZipPath);
            }
        }
        catch
        {
            // ignore
        }

        _packedZipPath = null;
    }
}
