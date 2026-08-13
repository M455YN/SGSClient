using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;
using SGSClient.Core.Models;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class GamePropertiesViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private bool _suppressVersionPersist;
    private string? _installPath;
    private string? _currentLibraryRoot;

    public GamePropertiesViewModel(ISgsApiService? api, IServiceScopeFactory? scopes)
    {
        _api = api;
        _scopes = scopes;
    }

    public ObservableCollection<PropertiesNavItem> NavItems { get; } =
    [
        new("general", "Ogólne"),
        new("updates", "Aktualizacje"),
        new("files", "Zainstalowane pliki"),
        new("betas", "Wersje i bety gry"),
        new("dlc", "DLC"),
        new("personalization", "Personalizacja")
    ];

    public ObservableCollection<BetaVersionItem> Versions { get; } = [];
    public ObservableCollection<MoveTargetFolderItem> MoveTargets { get; } = [];

    [ObservableProperty]
    public partial int GameId { get; set; }

    [ObservableProperty]
    public partial int BuildId { get; set; }

    [ObservableProperty]
    public partial string GameTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBetas))]
    [NotifyPropertyChangedFor(nameof(ShowPersonalization))]
    [NotifyPropertyChangedFor(nameof(ShowFiles))]
    [NotifyPropertyChangedFor(nameof(ShowPlaceholder))]
    public partial string SelectedSection { get; set; } = "betas";

    [ObservableProperty]
    public partial BetaVersionItem? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? CustomCoverPath { get; set; }

    [ObservableProperty]
    public partial string? CustomCoverPreviewUri { get; set; }

    [ObservableProperty]
    public partial bool HasCustomCover { get; set; }

    [ObservableProperty]
    public partial string InstallPathText { get; set; } = "—";

    [ObservableProperty]
    public partial string InstallSizeText { get; set; } = "—";

    [ObservableProperty]
    public partial string InstallFilesText { get; set; } = "—";

    [ObservableProperty]
    public partial string InstallStateText { get; set; } = "Niezainstalowana";

    [ObservableProperty]
    public partial string InstalledVersionText { get; set; } = "—";

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool CanBrowseFiles { get; set; }

    [ObservableProperty]
    public partial bool CanUninstallFiles { get; set; }

    [ObservableProperty]
    public partial bool IsUninstalling { get; set; }

    [ObservableProperty]
    public partial MoveTargetFolderItem? SelectedMoveTarget { get; set; }

    [ObservableProperty]
    public partial bool CanMoveFiles { get; set; }

    [ObservableProperty]
    public partial bool IsMoving { get; set; }

    [ObservableProperty]
    public partial string MoveProgressText { get; set; } = string.Empty;

    public bool ShowBetas => SelectedSection == "betas";
    public bool ShowPersonalization => SelectedSection == "personalization";
    public bool ShowFiles => SelectedSection == "files";
    public bool ShowPlaceholder => SelectedSection is not ("betas" or "personalization" or "files");
    public string PlaceholderText => "Ta sekcja pojawi się w kolejnej wersji klienta.";

    partial void OnSelectedSectionChanged(string value)
    {
        foreach (var nav in NavItems)
        {
            nav.IsSelected = nav.Id == value;
        }
    }

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
            CustomCoverPreviewUri = null;
            ClearInstallInfo();

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
        if (!string.IsNullOrWhiteSpace(sectionId))
        {
            SelectedSection = sectionId;
        }
    }

    [RelayCommand]
    private void SelectVersion(BetaVersionItem? item)
    {
        if (item is not null)
        {
            SelectedVersion = item;
        }
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
        HasCustomCover = !string.IsNullOrWhiteSpace(CustomCoverPath) && File.Exists(CustomCoverPath);
        CustomCoverPreviewUri = HasCustomCover
            ? new Uri(CustomCoverPath!).AbsoluteUri
            : null;
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
    public PropertiesNavItem(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
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
