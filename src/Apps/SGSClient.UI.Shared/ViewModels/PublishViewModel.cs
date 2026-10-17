using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.UI.ViewModels;

public partial class PublishViewModel : ObservableObject
{
    private readonly IGamePackagePacker? _packer;
    private readonly IGameUploadClient? _upload;
    private readonly IAuthSession? _auth;
    private readonly ISgsApiService? _api;
    private string? _packedZipPath;
    private SgsPackageHints? _packageHints;
    private string? _studioName;
    private string? _websiteUrl;
    private string? _discordUrl;
    private string? _supportUrl;
    private string? _releaseStatus;
    private string? _releaseDate;
    private string? _ageRating;
    private IList<string>? _features;
    private IList<string>? _languages;
    private IList<string>? _tags;

    public PublishViewModel()
        : this(null, null, null)
    {
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
        StatusText = Loc.T("Wybierz folder z grą (musi zawierać sgs-manifest.json).");
        RefreshAuthHint();
    }

    public string Title => Loc.T(Kind switch
    {
        "dlc" => "Publikuj DLC",
        "app" => "Publikuj program",
        "book" => "Publikuj książkę",
        "music" => "Publikuj muzykę",
        "art" => "Publikuj grafikę",
        _ => "Publikuj grę"
    });
    public ObservableCollection<string> Issues { get; } = [];
    public ObservableCollection<string> ParentGameOptions { get; } = [];
    public IReadOnlyList<string> PlatformOptions { get; } = ["windows", "linux", "macos"];
    public IReadOnlyList<string> ArchitectureOptions { get; } = ["x64", "x86", "arm64"];
    public TaxonomySelection GameGenres { get; } =
        new(SgsGameTaxonomy.Genres, SgsGameTaxonomy.MaxGenres, Loc.T("Wybierz gatunki"));
    public TaxonomySelection GameEngines { get; } =
        new(SgsGameTaxonomy.Engines, SgsGameTaxonomy.MaxEngines, Loc.T("Wybierz silniki"));

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedFolder { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasManifestForm { get; set; }
    [ObservableProperty] public partial bool IsDlc { get; set; }
    [ObservableProperty] public partial string ParentSymbol { get; set; } = string.Empty;
    [ObservableProperty] public partial string GameTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string GameSymbol { get; set; } = string.Empty;
    [ObservableProperty] public partial string GameDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial string HardwareRequirements { get; set; } = string.Empty;
    [ObservableProperty] public partial string OtherInformation { get; set; } = string.Empty;
    [ObservableProperty] public partial string MediaCover { get; set; } = string.Empty;
    [ObservableProperty] public partial string MediaHeader { get; set; } = string.Empty;
    [ObservableProperty] public partial string MediaLogo { get; set; } = string.Empty;
    [ObservableProperty] public partial string MediaIntro { get; set; } = string.Empty;
    [ObservableProperty] public partial string MediaScreenshots { get; set; } = string.Empty;
    [ObservableProperty] public partial string BuildVersion { get; set; } = string.Empty;
    [ObservableProperty] public partial string ReleaseNotes { get; set; } = string.Empty;
    [ObservableProperty] public partial string BuildPlatform { get; set; } = "windows";
    [ObservableProperty] public partial string BuildArchitecture { get; set; } = "x64";
    [ObservableProperty] public partial string BuildExecutable { get; set; } = string.Empty;
    [ObservableProperty] public partial string SummaryTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string SummaryMeta { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasSummary { get; set; }
    [ObservableProperty] public partial bool CanPublish { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial double Progress { get; set; }
    [ObservableProperty] public partial bool ShowProgress { get; set; }
    [ObservableProperty] public partial string AuthHint { get; set; } = string.Empty;

    /// <summary>Manifest kind: game, dlc, app, book, music or art. <see cref="IsDlc"/> mirrors kind == "dlc".</summary>
    [ObservableProperty] public partial string Kind { get; set; } = "game";

    public bool ShowParentField => IsDlc;

    partial void OnKindChanged(string value)
    {
        IsDlc = value == "dlc";
        OnPropertyChanged(nameof(Title));
    }

    partial void OnIsDlcChanged(bool value)
    {
        if (value != (Kind == "dlc"))
        {
            Kind = value ? "dlc" : "game";
        }

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
        var keepKind = Kind;
        var keepParent = ParentSymbol;
        ClearForm();
        Kind = keepKind;
        ParentSymbol = keepParent;
        _packageHints = null;

        if (string.IsNullOrWhiteSpace(folder))
        {
            StatusText = Loc.T("Wybierz folder z grą.");
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

                StatusText = Loc.T("Nie udało się wczytać manifestu z folderu.");
                return;
            }

            ApplyManifestToForm(loaded.Manifest, preferExistingDlc: keepKind == "dlc");
            HasManifestForm = true;
            StatusText = Loc.T("Uzupełnij lub popraw dane gry, potem przygotuj paczkę.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Błąd odczytu manifestu: {0}", ex.Message);
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
            AuthHint = Loc.T("Logowanie jest wyłączone — publikacja nastąpi bez konta.");
        }
        else
        {
            AuthHint = _auth.IsLoggedIn
                ? $"Zalogowano jako {_auth.CurrentUser?.DisplayName ?? "gracz"}."
                : Loc.T("Zaloguj się w Profilu przed publikacją.");
        }

        _ = LoadParentOptionsAsync();
    }

    public void PrepareForDlc(string parentSymbol)
    {
        Kind = "dlc";
        ParentSymbol = parentSymbol?.Trim() ?? string.Empty;
        StatusText = string.IsNullOrWhiteSpace(ParentSymbol)
            ? Loc.T("Tryb DLC — wybierz grę bazową i folder z paczką.")
            : Loc.F("Tryb DLC dla gry bazowej: {0}. Wybierz folder z paczką.", ParentSymbol);
        _ = LoadParentOptionsAsync();
    }

    public void PrepareForNewGame()
    {
        Kind = "game";
        ParentSymbol = string.Empty;
        StatusText = Loc.T("Wybierz folder z grą (musi zawierać sgs-manifest.json).");
    }

    public void PrepareForApp()
    {
        Kind = "app";
        ParentSymbol = string.Empty;
        StatusText = Loc.T("Wybierz folder z programem (musi zawierać sgs-manifest.json).");
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

            if (IsDlc
                && !string.IsNullOrWhiteSpace(ParentSymbol)
                && !ParentGameOptions.Contains(ParentSymbol, StringComparer.OrdinalIgnoreCase)
                && ParentGameOptions.Count > 0)
            {
                // keep typed/deep-linked value even if not yet in list
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
        StatusText = Loc.T("Zapisuję dane i pakuję grę…");
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

                StatusText = Loc.T("Nie udało się przygotować paczki.");
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
            StatusText = Loc.T("Paczka gotowa (manifest zapisany w folderze). Możesz ją wysłać do sklepu.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Błąd pakowania: {0}", ex.Message);
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
        StatusText = Loc.T("Wysyłanie do sklepu…");
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

                StatusText = Loc.T("Publikacja nie powiodła się.");
                return;
            }

            StatusText = Loc.F("Opublikowano: {0}. Znajdziesz ją w Sklepie.", result.Game.Title);
            CanPublish = false;
            CleanupPackedZip();
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Błąd wysyłania: {0}", ex.Message);
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
        StatusText = Loc.F("Ustawiono miniaturkę: {0}", relative);
        return true;
    }

    public bool TrySetHeaderFromFile(string sourcePath, out string? error)
    {
        if (!TryImportMediaFile(sourcePath, SgsPackageRules.AllowedImageExtensions, "header", out var relative, out error))
        {
            return false;
        }

        MediaHeader = relative;
        StatusText = Loc.F("Ustawiono okładkę poziomą: {0}", relative);
        return true;
    }

    public bool TrySetLogoFromFile(string sourcePath, out string? error)
    {
        if (!TryImportMediaFile(sourcePath, SgsPackageRules.AllowedImageExtensions, "logo", out var relative, out error))
        {
            return false;
        }

        MediaLogo = relative;
        StatusText = Loc.F("Ustawiono logo: {0}", relative);
        return true;
    }

    public bool TrySetIntroFromFile(string sourcePath, out string? error)
    {
        if (!TryImportMediaFile(sourcePath, SgsPackageRules.AllowedVideoExtensions, "intro", out var relative, out error))
        {
            return false;
        }

        MediaIntro = relative;
        StatusText = Loc.F("Ustawiono intro: {0}", relative);
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
            error = Loc.T("Nie wybrano plików.");
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
            error = Loc.F("Maksymalnie {0} screenów.", SgsPackageRules.MaxScreenshots);
            return false;
        }

        MediaScreenshots = string.Join(Environment.NewLine, existing);
        StatusText = Loc.F("Dodano screeny ({0}). Łącznie: {1}.", added.Count, existing.Count);
        return true;
    }

    public void ClearCover()
    {
        MediaCover = string.Empty;
        StatusText = Loc.T("Usunięto miniaturkę z formularza.");
    }

    public void ClearHeader()
    {
        MediaHeader = string.Empty;
        StatusText = Loc.T("Usunięto okładkę poziomą z formularza.");
    }

    public void ClearLogo()
    {
        MediaLogo = string.Empty;
        StatusText = Loc.T("Usunięto logo z formularza.");
    }

    public void ClearIntro()
    {
        MediaIntro = string.Empty;
        StatusText = Loc.T("Usunięto intro z formularza.");
    }

    public void ClearScreenshots()
    {
        MediaScreenshots = string.Empty;
        StatusText = Loc.T("Usunięto listę screenów z formularza.");
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
            error = Loc.T("Najpierw wybierz folder gry.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            error = Loc.T("Nie znaleziono wybranego pliku.");
            return false;
        }

        var ext = Path.GetExtension(sourcePath);
        if (!allowedExtensions.Contains(ext))
        {
            error = Loc.F("Niedozwolone rozszerzenie '{0}'. Dozwolone: {1}.", ext, string.Join(", ", allowedExtensions));
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
            error = Loc.F("Nie udało się skopiować pliku do folderu gry: {0}", ex.Message);
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
            Kind = "dlc";
            ParentSymbol = string.IsNullOrWhiteSpace(manifest.Game.ParentSymbol)
                ? ParentSymbol
                : manifest.Game.ParentSymbol!;
        }
        else if (!preferExistingDlc || !string.IsNullOrWhiteSpace(rawKind))
        {
            Kind = kind ?? "game";
            ParentSymbol = string.Empty;
        }

        GameTitle = manifest.Game.Title ?? string.Empty;
        GameSymbol = manifest.Game.Symbol ?? string.Empty;
        GameDescription = manifest.Game.Description ?? string.Empty;
        HardwareRequirements = manifest.Game.HardwareRequirements ?? string.Empty;
        OtherInformation = manifest.Game.OtherInformation ?? string.Empty;
        GameGenres.Set(SgsGameTaxonomy.MergeSlugs(manifest.Game.Genre, manifest.Game.Genres));
        GameEngines.Set(SgsGameTaxonomy.MergeSlugs(manifest.Game.Engine, manifest.Game.Engines));
        _studioName = manifest.Game.StudioName;
        _websiteUrl = manifest.Game.WebsiteUrl;
        _discordUrl = manifest.Game.DiscordUrl;
        _supportUrl = manifest.Game.SupportUrl;
        _releaseStatus = manifest.Game.ReleaseStatus;
        _releaseDate = manifest.Game.ReleaseDate;
        _ageRating = manifest.Game.AgeRating;
        _features = manifest.Game.Features?.ToList();
        _languages = manifest.Game.Languages?.ToList();
        _tags = manifest.Game.Tags?.ToList();
        MediaCover = manifest.Game.Media?.Cover ?? string.Empty;
        MediaHeader = manifest.Game.Media?.Header ?? string.Empty;
        MediaLogo = manifest.Game.Media?.Logo ?? string.Empty;
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
            || !string.IsNullOrWhiteSpace(MediaLogo)
            || !string.IsNullOrWhiteSpace(MediaIntro)
            || screenshots.Count > 0)
        {
            media = new SgsPackageMediaInfo
            {
                Cover = NullIfWhiteSpace(MediaCover),
                Header = NullIfWhiteSpace(MediaHeader),
                Logo = NullIfWhiteSpace(MediaLogo),
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
                Kind = Kind,
                ParentSymbol = IsDlc ? NullIfWhiteSpace(ParentSymbol) : null,
                Description = NullIfWhiteSpace(GameDescription),
                HardwareRequirements = NullIfWhiteSpace(HardwareRequirements),
                OtherInformation = NullIfWhiteSpace(OtherInformation),
                Genre = GameGenres.SelectedSymbols.FirstOrDefault(),
                Genres = GameGenres.SelectedSymbols.ToList(),
                Engine = GameEngines.SelectedSymbols.FirstOrDefault(),
                Engines = GameEngines.SelectedSymbols.ToList(),
                StudioName = NullIfWhiteSpace(_studioName),
                WebsiteUrl = NullIfWhiteSpace(_websiteUrl),
                DiscordUrl = NullIfWhiteSpace(_discordUrl),
                SupportUrl = NullIfWhiteSpace(_supportUrl),
                ReleaseStatus = NullIfWhiteSpace(_releaseStatus),
                ReleaseDate = NullIfWhiteSpace(_releaseDate),
                AgeRating = NullIfWhiteSpace(_ageRating),
                Features = _features,
                Languages = _languages,
                Tags = _tags,
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
        Kind = "game";
        ParentSymbol = string.Empty;
        GameTitle = string.Empty;
        GameSymbol = string.Empty;
        GameDescription = string.Empty;
        HardwareRequirements = string.Empty;
        OtherInformation = string.Empty;
        GameGenres.Set(null);
        GameEngines.Set(null);
        _studioName = null;
        _websiteUrl = null;
        _discordUrl = null;
        _supportUrl = null;
        _releaseStatus = null;
        _releaseDate = null;
        _ageRating = null;
        _features = null;
        _languages = null;
        _tags = null;
        MediaCover = string.Empty;
        MediaHeader = string.Empty;
        MediaLogo = string.Empty;
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
