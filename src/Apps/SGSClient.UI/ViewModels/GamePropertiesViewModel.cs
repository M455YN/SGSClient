using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;
using SGSClient.Core.Models;

namespace SGSClient.UI.ViewModels;

public partial class GamePropertiesViewModel : ViewModelBase
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private bool _suppressVersionPersist;
    private string? _installPath;
    private string? _currentLibraryRoot;

    public GamePropertiesViewModel()
    {
        StatusText = "Brak API";
    }

    public GamePropertiesViewModel(ISgsApiService? api, IServiceScopeFactory? scopes)
    {
        _api = api;
        _scopes = scopes;
    }

    public ObservableCollection<PropertiesNavItem> NavItems { get; } =
    [
        new("general", "Ogólne", Implemented: false),
        new("updates", "Aktualizacje", Implemented: false),
        new("files", "Zainstalowane pliki", Implemented: true),
        new("betas", "Wersje i bety gry", Implemented: true),
        new("dlc", "DLC", Implemented: true),
        new("personalization", "Personalizacja", Implemented: true)
    ];

    public ObservableCollection<BetaVersionItem> Versions { get; } = [];
    public ObservableCollection<MoveTargetFolderItem> MoveTargets { get; } = [];

    [ObservableProperty]
    private int _gameId;

    [ObservableProperty]
    private int _buildId;

    [ObservableProperty]
    private string _gameTitle = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _selectedSection = "betas";

    [ObservableProperty]
    private BetaVersionItem? _selectedVersion;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _customCoverPath;

    [ObservableProperty]
    private Bitmap? _customCoverPreview;

    [ObservableProperty]
    private bool _hasCustomCover;

    [ObservableProperty]
    private string _installPathText = "—";

    [ObservableProperty]
    private string _installSizeText = "—";

    [ObservableProperty]
    private string _installFilesText = "—";

    [ObservableProperty]
    private string _installStateText = "Niezainstalowana";

    [ObservableProperty]
    private string _installedVersionText = "—";

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _canBrowseFiles;

    [ObservableProperty]
    private bool _canUninstallFiles;

    [ObservableProperty]
    private bool _isUninstalling;

    [ObservableProperty]
    private MoveTargetFolderItem? _selectedMoveTarget;

    [ObservableProperty]
    private bool _canMoveFiles;

    [ObservableProperty]
    private bool _isMoving;

    [ObservableProperty]
    private string _moveProgressText = string.Empty;

    [ObservableProperty]
    private string _dlcSearchText = string.Empty;

    [ObservableProperty]
    private string _dlcStatusText = string.Empty;

    public ObservableCollection<DlcRowItem> DlcRows { get; } = [];
    public ObservableCollection<DlcRowItem> FilteredDlcRows { get; } = [];

    public event EventHandler? ViewInStoreRequested;

    public bool ShowBetas => SelectedSection == "betas";
    public bool ShowPersonalization => SelectedSection == "personalization";
    public bool ShowFiles => SelectedSection == "files";
    public bool ShowDlc => SelectedSection == "dlc";
    public bool ShowPlaceholder => SelectedSection is not ("betas" or "personalization" or "files" or "dlc");
    public string PlaceholderText => "Ta sekcja pojawi się w kolejnej wersji klienta.";

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(ShowBetas));
        OnPropertyChanged(nameof(ShowPersonalization));
        OnPropertyChanged(nameof(ShowFiles));
        OnPropertyChanged(nameof(ShowDlc));
        OnPropertyChanged(nameof(ShowPlaceholder));
        foreach (var nav in NavItems)
        {
            nav.IsSelected = nav.Id == value;
        }

        if (value == "dlc" && DlcRows.Count == 0 && GameId > 0)
        {
            _ = LoadDlcsAsync();
        }
    }

    partial void OnDlcSearchTextChanged(string value) => ApplyDlcFilter();

    partial void OnSelectedVersionChanged(BetaVersionItem? value)
    {
        if (_suppressVersionPersist || value is null)
        {
            return;
        }

        _ = PersistTargetVersionAsync(value);
    }

    public async Task LoadAsync(int gameId, int buildId, string title)
    {
        GameId = gameId;
        BuildId = buildId;
        GameTitle = title.ToUpperInvariant();
        SelectedSection = "betas";
        StatusText = "Ładowanie…";
        IsBusy = true;

        try
        {
            Versions.Clear();
            string? savedTarget = null;
            CustomCoverPath = null;
            HasCustomCover = false;
            CustomCoverPreview = null;
            ClearInstallInfo();
            DlcRows.Clear();
            FilteredDlcRows.Clear();
            DlcSearchText = string.Empty;
            DlcStatusText = string.Empty;

            if (_scopes is not null)
            {
                using var scope = _scopes.CreateScope();
                var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
                var item = await library.GetByGameIdAsync(gameId);
                savedTarget = item?.TargetVersion;
                CustomCoverPath = item?.CustomCoverPath;
                if (item is not null && item.BuildId > 0)
                {
                    BuildId = item.BuildId;
                }

                ApplyInstallInfo(item);
            }

            RefreshCoverPreview();

            IReadOnlyList<GameBuildVersionDto> history = [];
            if (_api is not null && BuildId > 0)
            {
                history = await _api.GetBuildVersionsAsync(gameId, BuildId);
            }

            _suppressVersionPersist = true;
            if (history.Count == 0)
            {
                var fallback = BetaVersionItem.CreatePublicDefault(savedTarget ?? "current");
                Versions.Add(fallback);
                SelectedVersion = fallback;
            }
            else
            {
                foreach (var version in history
                             .OrderByDescending(v => v.IsCurrent)
                             .ThenByDescending(v => v.PublishedAtUtc))
                {
                    Versions.Add(BetaVersionItem.FromDto(version));
                }

                SelectedVersion =
                    Versions.FirstOrDefault(v =>
                        !string.IsNullOrWhiteSpace(savedTarget)
                        && string.Equals(v.Version, savedTarget, StringComparison.OrdinalIgnoreCase))
                    ?? Versions.FirstOrDefault(v => v.IsCurrent)
                    ?? Versions.FirstOrDefault();
            }

            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd: {ex.Message}";
        }
        finally
        {
            _suppressVersionPersist = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectSection(string? sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            return;
        }

        SelectedSection = sectionId;
    }

    [RelayCommand]
    private void SelectVersion(BetaVersionItem? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedVersion = item;
    }

    [RelayCommand]
    private void BrowseLocalFiles()
    {
        if (!CanBrowseFiles || string.IsNullOrWhiteSpace(_installPath) || !Directory.Exists(_installPath))
        {
            StatusText = "Folder instalacji nie jest dostępny.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _installPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się otworzyć folderu: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task UninstallFilesAsync()
    {
        if (_scopes is null || GameId <= 0 || !CanUninstallFiles || IsUninstalling)
        {
            return;
        }

        IsUninstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var installer = scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            var result = await installer.UninstallAsync(model);
            if (!result.Success)
            {
                StatusText = result.Errors.FirstOrDefault() ?? "Odinstalowanie nie powiodło się.";
                return;
            }

            await library.UpsertAsync(model);
            ApplyInstallInfo(model);
            StatusText = "Odinstalowano pliki lokalne. Gra pozostaje w bibliotece.";
        }
        catch (Exception ex)
        {
            StatusText = $"Odinstalowanie nie powiodło się: {ex.Message}";
        }
        finally
        {
            IsUninstalling = false;
        }
    }

    [RelayCommand]
    private async Task MoveFilesAsync()
    {
        if (_scopes is null || GameId <= 0 || !CanMoveFiles || IsMoving || SelectedMoveTarget is null)
        {
            return;
        }

        IsMoving = true;
        MoveProgressText = "Przenoszenie plików…";
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var installer = scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            var progress = new Progress<InstallProgress>(p =>
            {
                MoveProgressText = p.Message ?? "Przenoszenie…";
            });

            var result = await installer.MoveAsync(model, SelectedMoveTarget.Path, progress);
            if (!result.Success)
            {
                StatusText = result.Errors.FirstOrDefault() ?? "Przenoszenie nie powiodło się.";
                return;
            }

            await library.UpsertAsync(model);
            ApplyInstallInfo(model);
            StatusText = $"Przeniesiono do: {SelectedMoveTarget.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusText = $"Przenoszenie nie powiodło się: {ex.Message}";
        }
        finally
        {
            IsMoving = false;
            MoveProgressText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task RefreshInstallInfoAsync()
    {
        if (_scopes is null || GameId <= 0)
        {
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var item = await library.GetByGameIdAsync(GameId);
            ApplyInstallInfo(item);
            StatusText = "Odświeżono informacje o plikach.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się odświeżyć: {ex.Message}";
        }
    }

    public async Task ApplyCustomCoverFromFileAsync(string sourcePath)
    {
        if (_scopes is null || GameId <= 0)
        {
            return;
        }

        try
        {
            var savedPath = await GameArtworkStore.SaveCustomCoverAsync(GameId, sourcePath);
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            model.CustomCoverPath = savedPath;
            await library.UpsertAsync(model);
            CustomCoverPath = savedPath;
            RefreshCoverPreview();
            StatusText = "Ustawiono własny obrazek kafla.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się ustawić obrazka: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearCustomCoverAsync()
    {
        if (_scopes is null || GameId <= 0)
        {
            return;
        }

        try
        {
            GameArtworkStore.ClearCustomCover(GameId, CustomCoverPath);
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                return;
            }

            model.CustomCoverPath = null;
            await library.UpsertAsync(model);
            CustomCoverPath = null;
            RefreshCoverPreview();
            StatusText = "Przywrócono domyślny obrazek kafla.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się wyczyścić obrazka: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ViewMoreInStore() => ViewInStoreRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task ToggleDlcInstallAsync(DlcRowItem? row)
    {
        if (row is null)
        {
            return;
        }

        await SetDlcInstalledAsync(row, !row.IsInstalled);
    }

    public async Task SetDlcInstalledAsync(DlcRowItem row, bool wantInstalled)
    {
        if (_scopes is null || row.IsBusy || row.PreferredBuildId <= 0)
        {
            return;
        }

        if (row.IsInstalled == wantInstalled)
        {
            return;
        }

        row.IsBusy = true;
        DlcStatusText = string.Empty;
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var installer = scope.ServiceProvider.GetRequiredService<IGameInstallService>();

            if (!wantInstalled)
            {
                var model = await library.GetByGameIdAsync(row.Id);
                if (model is null)
                {
                    row.IsInstalled = false;
                    return;
                }

                var result = await installer.UninstallAsync(model);
                if (!result.Success)
                {
                    DlcStatusText = result.Errors.FirstOrDefault() ?? "Nie udało się odinstalować DLC.";
                    row.IsInstalled = true;
                    return;
                }

                await library.UpsertAsync(model);
                row.IsInstalled = false;
                DlcStatusText = $"Odinstalowano: {row.Title}";
                return;
            }

            var existing = await library.GetByGameIdAsync(row.Id);
            var item = existing ?? new GameLibraryItem
            {
                GameId = row.Id,
                BuildId = row.PreferredBuildId,
                Symbol = row.Symbol,
                Title = row.Title,
                Platform = row.PreferredPlatform,
                Architecture = row.PreferredArchitecture,
                CoverUrl = row.CoverUrl,
                InstallState = InstallState.NotInstalled
            };

            if (existing is null)
            {
                await library.UpsertAsync(item);
            }

            var install = await installer.InstallAsync(item);
            if (!install.Success)
            {
                DlcStatusText = install.Errors.FirstOrDefault()
                    ?? "Nie udało się zainstalować DLC.";
                row.IsInstalled = false;
                return;
            }

            await library.UpsertAsync(item);
            row.IsInstalled = true;
            DlcStatusText = $"Zainstalowano: {row.Title}";
        }
        catch (Exception ex)
        {
            DlcStatusText = ex.Message;
            row.IsInstalled = !wantInstalled;
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    private async Task LoadDlcsAsync()
    {
        if (_api is null || GameId <= 0)
        {
            DlcStatusText = "Brak API.";
            return;
        }

        try
        {
            DlcStatusText = "Ładowanie DLC…";
            var apiBase = string.Empty;
            if (_scopes is not null)
            {
                using var scope = _scopes.CreateScope();
                apiBase = scope.ServiceProvider
                    .GetService<Microsoft.Extensions.Options.IOptions<SGSClient.Core.Configuration.ApiClientSettings>>()
                    ?.Value.BaseUrl ?? string.Empty;
            }

            var dlcs = await _api.GetGameDlcsAsync(GameId);
            DlcRows.Clear();
            foreach (var dlc in dlcs.OrderBy(d => d.Title))
            {
                var installed = false;
                if (_scopes is not null)
                {
                    using var scope = _scopes.CreateScope();
                    var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
                    var item = await library.GetByGameIdAsync(dlc.Id);
                    installed = item is not null
                        && item.InstallState == InstallState.Installed
                        && !string.IsNullOrWhiteSpace(item.InstallPath)
                        && Directory.Exists(item.InstallPath);
                }

                var cover = string.IsNullOrWhiteSpace(dlc.CoverUrl)
                    ? null
                    : InstallPathHelper.ResolveDownloadUrl(apiBase, dlc.CoverUrl);
                DlcRows.Add(DlcRowItem.FromDto(dlc, installed, cover));
            }

            ApplyDlcFilter();
            DlcStatusText = DlcRows.Count == 0 ? "Brak DLC dla tej gry." : string.Empty;
        }
        catch (Exception ex)
        {
            DlcStatusText = $"Błąd DLC: {ex.Message}";
        }
    }

    private void ApplyDlcFilter()
    {
        FilteredDlcRows.Clear();
        var q = DlcSearchText?.Trim() ?? string.Empty;
        foreach (var row in DlcRows)
        {
            if (q.Length == 0
                || row.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                || row.Symbol.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                FilteredDlcRows.Add(row);
            }
        }
    }

    private void ClearInstallInfo()
    {
        _installPath = null;
        _currentLibraryRoot = null;
        InstallPathText = "—";
        InstallSizeText = "—";
        InstallFilesText = "—";
        InstallStateText = "Niezainstalowana";
        InstalledVersionText = "—";
        IsInstalled = false;
        CanBrowseFiles = false;
        CanUninstallFiles = false;
        CanMoveFiles = false;
        SelectedMoveTarget = null;
        MoveTargets.Clear();
    }

    private void ApplyInstallInfo(GameLibraryItem? item)
    {
        if (item is null)
        {
            ClearInstallInfo();
            return;
        }

        _installPath = item.InstallPath;
        var folderExists = !string.IsNullOrWhiteSpace(_installPath) && Directory.Exists(_installPath);

        IsInstalled = item.InstallState == InstallState.Installed && folderExists;
        CanBrowseFiles = folderExists;
        CanUninstallFiles = item.InstallState == InstallState.Installed;
        InstallPathText = folderExists
            ? _installPath!
            : string.IsNullOrWhiteSpace(_installPath)
                ? "—"
                : $"{_installPath} (brak folderu)";
        InstalledVersionText = string.IsNullOrWhiteSpace(item.InstalledVersion) ? "—" : item.InstalledVersion!;
        InstallStateText = item.InstallState switch
        {
            InstallState.Installed when folderExists => "Zainstalowana",
            InstallState.Installed => "Zainstalowana (folder niedostępny)",
            InstallState.Downloading => "Pobieranie…",
            InstallState.Installing => "Instalowanie…",
            InstallState.Failed => "Błąd instalacji",
            _ => "Niezainstalowana"
        };

        if (!folderExists)
        {
            InstallSizeText = "—";
            InstallFilesText = "—";
            RefreshMoveTargets(null);
            return;
        }

        var stats = InstallFolderInfo.Measure(_installPath);
        InstallSizeText = InstallFolderInfo.FormatSize(stats.Bytes);
        InstallFilesText = stats.FileCount == 1 ? "1 plik" : $"{stats.FileCount} plików";
        RefreshMoveTargets(_installPath);
    }

    private void RefreshMoveTargets(string? installPath)
    {
        MoveTargets.Clear();
        SelectedMoveTarget = null;
        CanMoveFiles = false;
        _currentLibraryRoot = null;

        if (_scopes is null || string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
        {
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var paths = scope.ServiceProvider.GetRequiredService<ILibraryPathProvider>();
            var folders = paths.GetLibraryFolders()
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fullInstall = Path.GetFullPath(installPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _currentLibraryRoot = folders
                .OrderByDescending(r => r.Length)
                .FirstOrDefault(root =>
                {
                    var fullRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return fullInstall.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                           || fullInstall.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                });

            foreach (var folder in folders)
            {
                if (_currentLibraryRoot is not null
                    && string.Equals(folder, _currentLibraryRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                MoveTargets.Add(new MoveTargetFolderItem
                {
                    Path = folder,
                    DisplayName = folder
                });
            }

            SelectedMoveTarget = MoveTargets.FirstOrDefault();
            CanMoveFiles = IsInstalled && MoveTargets.Count > 0;
        }
        catch
        {
            CanMoveFiles = false;
        }
    }

    private void RefreshCoverPreview()
    {
        CustomCoverPreview = null;
        HasCustomCover = !string.IsNullOrWhiteSpace(CustomCoverPath) && File.Exists(CustomCoverPath);
        if (!HasCustomCover)
        {
            return;
        }

        try
        {
            CustomCoverPreview = new Bitmap(CustomCoverPath!);
        }
        catch
        {
            CustomCoverPreview = null;
            HasCustomCover = false;
        }
    }

    private async Task PersistTargetVersionAsync(BetaVersionItem selected)
    {
        if (_scopes is null || GameId <= 0)
        {
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                return;
            }

            model.TargetVersion = selected.Version;
            await library.UpsertAsync(model);
            StatusText = selected.IsCurrent
                ? "Ustawiono domyślną wersję publiczną."
                : $"Wybrano wersję {selected.Version}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się zapisać: {ex.Message}";
        }
    }
}

public partial class PropertiesNavItem : ObservableObject
{
    public PropertiesNavItem(string id, string label, bool Implemented)
    {
        Id = id;
        Label = label;
        this.Implemented = Implemented;
    }

    public string Id { get; }
    public string Label { get; }
    public bool Implemented { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed class BetaVersionItem
{
    public required string Version { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string UpdatedText { get; init; }
    public bool IsCurrent { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }

    public static BetaVersionItem CreatePublicDefault(string version) => new()
    {
        Version = version,
        Title = "Domyślna wersja publiczna",
        Description = "Najpowszechniejsza wersja gry",
        UpdatedText = "—",
        IsCurrent = true,
        PublishedAtUtc = DateTimeOffset.UtcNow
    };

    public static BetaVersionItem FromDto(GameBuildVersionDto dto) => new()
    {
        Version = dto.Version,
        Title = dto.IsCurrent ? "Domyślna wersja publiczna" : $"Wersja {dto.Version}",
        Description = dto.IsCurrent
            ? "Najpowszechniejsza wersja gry"
            : $"Historyczna paczka v{dto.Version}",
        UpdatedText = dto.PublishedAtUtc.LocalDateTime.ToString("d MMM yyyy"),
        IsCurrent = dto.IsCurrent,
        PublishedAtUtc = dto.PublishedAtUtc
    };
}

public sealed class MoveTargetFolderItem
{
    public required string Path { get; init; }
    public required string DisplayName { get; init; }

    public override string ToString() => DisplayName;
}

public partial class DlcRowItem : ObservableObject
{
    public int Id { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? CoverPreviewUri { get; init; }
    public string? CoverUrl { get; init; }
    public int PreferredBuildId { get; init; }
    public GamePlatform PreferredPlatform { get; init; }
    public string? PreferredArchitecture { get; init; }
    public string AddedText { get; init; } = "—";
    public string SizeText { get; init; } = "—";

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isBusy;

    public static DlcRowItem FromDto(GameDlcDto dto, bool installed, string? coverPreview) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        Symbol = dto.Symbol,
        CoverUrl = dto.CoverUrl,
        CoverPreviewUri = coverPreview,
        PreferredBuildId = dto.PreferredBuildId,
        PreferredPlatform = dto.PreferredPlatform,
        PreferredArchitecture = dto.PreferredArchitecture,
        AddedText = dto.CreatedAtUtc.LocalDateTime.ToString("d MMM yyyy"),
        SizeText = InstallFolderInfo.FormatSize(dto.PackageSizeBytes),
        IsInstalled = installed
    };
}
