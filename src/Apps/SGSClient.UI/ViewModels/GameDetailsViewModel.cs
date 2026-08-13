using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;

namespace SGSClient.UI.ViewModels;

public partial class GameDetailsViewModel : ViewModelBase
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private Action? _close;
    private string? _coverUrl;
    private GameLibraryItem? _libraryItem;
    private Action<GameLibraryItem>? _openProperties;

    public GameDetailsViewModel()
    {
        StatusText = "Brak API";
    }

    public GameDetailsViewModel(ISgsApiService? api, IServiceScopeFactory? scopes, Action? close = null)
    {
        _api = api;
        _scopes = scopes;
        _close = close;
        StatusText = "Ładowanie…";
    }

    public void SetCloseHandler(Action close) => _close = close;
    public void SetOpenPropertiesHandler(Action<GameLibraryItem> open) => _openProperties = open;

    public ObservableCollection<string> Badges { get; } = [];
    public ObservableCollection<MediaThumbItem> MediaThumbs { get; } = [];
    public ObservableCollection<BuildRowItem> Builds { get; } = [];

    [ObservableProperty]
    private int _gameId;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _symbol = string.Empty;

    [ObservableProperty]
    private string _tileLabel = string.Empty;

    [ObservableProperty]
    private string _tagline = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _hardwareRequirements = string.Empty;

    [ObservableProperty]
    private string _otherInformation = string.Empty;

    [ObservableProperty]
    private string _platformsText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _selectedTab = "Details";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasContent;

    [ObservableProperty]
    private bool _inLibrary;

    [ObservableProperty]
    private bool _canAdd;

    [ObservableProperty]
    private string _primaryActionLabel = "Dodaj do biblioteki";

    [ObservableProperty]
    private bool _showFeaturedBadge;

    [ObservableProperty]
    private bool _showHardware;

    [ObservableProperty]
    private bool _showOther;

    [ObservableProperty]
    private bool _showBuilds;

    [ObservableProperty]
    private bool _fromLibrary;

    [ObservableProperty]
    private string _backLabel = "← Sklep";

    [ObservableProperty]
    private string _contextLabel = "SGS STORE";

    [ObservableProperty]
    private bool _canLaunch;

    [ObservableProperty]
    private bool _canInstall;

    [ObservableProperty]
    private bool _canUninstall;

    [ObservableProperty]
    private bool _showLibraryActions;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private double _installProgressFraction;

    [ObservableProperty]
    private string _installProgressText = string.Empty;

    public bool ShowDetailsTab => SelectedTab == "Details";
    public bool ShowReviewsTab => SelectedTab == "Reviews";
    public bool ShowMoreTab => SelectedTab == "More";

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(ShowDetailsTab));
        OnPropertyChanged(nameof(ShowReviewsTab));
        OnPropertyChanged(nameof(ShowMoreTab));
    }

    public async Task LoadAsync(int gameId, bool fromLibrary = false)
    {
        GameId = gameId;
        FromLibrary = fromLibrary;
        BackLabel = fromLibrary ? "← Biblioteka" : "← Sklep";
        ContextLabel = fromLibrary ? "BIBLIOTEKA" : "SGS STORE";
        if (_api is null)
        {
            StatusText = "Brak klienta API.";
            HasContent = false;
            return;
        }

        IsBusy = true;
        HasContent = false;
        StatusText = "Ładowanie szczegółów…";
        try
        {
            var game = await _api.GetGameAsync(gameId);
            if (game is null)
            {
                StatusText = "Nie znaleziono gry.";
                return;
            }

            ApplyGame(game);
            await EnrichBuildHistoryAsync(game.Id);
            await RefreshLibraryStateAsync(game.Id);
            UpdateActionState(game);
            HasContent = true;
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd: {ex.Message}";
            HasContent = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close() => _close?.Invoke();

    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            SelectedTab = tab;
        }
    }

    [RelayCommand]
    private async Task AddToLibraryAsync()
    {
        if (!CanAdd || _scopes is null || GameId <= 0)
        {
            return;
        }

        var preferred = Builds.FirstOrDefault(b => b.IsPreferred) ?? Builds.FirstOrDefault();
        if (preferred is null || preferred.BuildId <= 0)
        {
            StatusText = "Brak builda dla tej platformy.";
            return;
        }

        var selectedVersion = preferred.SelectedVersion?.Version ?? preferred.Version;

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            await library.UpsertAsync(new GameLibraryItem
            {
                GameId = GameId,
                BuildId = preferred.BuildId,
                Symbol = Symbol,
                Title = Title,
                Platform = preferred.Platform,
                Architecture = preferred.Architecture,
                InstallState = InstallState.NotInstalled,
                InstalledVersion = null,
                TargetVersion = selectedVersion,
                CoverUrl = _coverUrl,
                ExecutablePath = null,
                InstallPath = null
            });

            InLibrary = true;
            CanAdd = false;
            PrimaryActionLabel = "W bibliotece";
            StatusText = preferred.SelectedVersion?.IsCurrent == true || preferred.SelectedVersion is null
                ? "Dodano do biblioteki (najnowsza wersja)."
                : $"Dodano do biblioteki (wersja {selectedVersion}).";
            await RefreshLibraryStateAsync(GameId);
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się dodać: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        if (_scopes is null || _libraryItem is null || string.IsNullOrWhiteSpace(_libraryItem.ExecutablePath))
        {
            StatusText = "Gra nie jest zainstalowana.";
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var launcher = scope.ServiceProvider.GetRequiredService<IGameLauncherService>();
            await launcher.LaunchAsync(_libraryItem.ExecutablePath);
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            _libraryItem.LastPlayedUtc = DateTimeOffset.UtcNow;
            await library.UpsertAsync(_libraryItem);
            StatusText = "Uruchomiono.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się uruchomić: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (_scopes is null || _libraryItem is null || IsInstalling)
        {
            return;
        }

        IsInstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var installer = scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            var progress = new Progress<InstallProgress>(p =>
            {
                InstallProgressFraction = p.Fraction;
                InstallProgressText = p.Message ?? string.Empty;
            });

            var result = await installer.InstallAsync(model, version: model.TargetVersion, progress: progress);
            await library.UpsertAsync(model);
            _libraryItem = model;
            ApplyLibraryActions(model);
            StatusText = result.Success
                ? $"Zainstalowano: {model.Title}"
                : result.Errors.FirstOrDefault() ?? "Instalacja nie powiodła się.";
        }
        catch (Exception ex)
        {
            StatusText = $"Instalacja nie powiodła się: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
            InstallProgressFraction = 0;
            InstallProgressText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (_scopes is null || _libraryItem is null || IsInstalling)
        {
            return;
        }

        IsInstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var installer = scope.ServiceProvider.GetRequiredService<IGameInstallService>();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            var result = await installer.UninstallAsync(model);
            await library.UpsertAsync(model);
            _libraryItem = model;
            ApplyLibraryActions(model);
            StatusText = result.Success
                ? $"Odinstalowano: {model.Title}"
                : result.Errors.FirstOrDefault() ?? "Odinstalowanie nie powiodło się.";
        }
        catch (Exception ex)
        {
            StatusText = $"Odinstalowanie nie powiodło się: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private void OpenProperties()
    {
        if (_libraryItem is null)
        {
            return;
        }

        _openProperties?.Invoke(_libraryItem);
    }

    private void ApplyGame(GameDto game)
    {
        Title = game.Title;
        Symbol = game.Symbol;
        _coverUrl = game.CoverUrl;
        TileLabel = string.IsNullOrWhiteSpace(game.Symbol)
            ? game.Title[..Math.Min(2, game.Title.Length)].ToUpperInvariant()
            : game.Symbol[..Math.Min(3, game.Symbol.Length)].ToUpperInvariant();
        Tagline = string.IsNullOrWhiteSpace(game.Description)
            ? "Poznaj szczegóły tej gry."
            : game.Description.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() is { Length: > 0 } first
                ? first + "."
                : game.Description!;
        Description = string.IsNullOrWhiteSpace(game.Description) ? "Brak opisu." : game.Description!;
        HardwareRequirements = string.IsNullOrWhiteSpace(game.HardwareRequirements)
            ? string.Empty
            : game.HardwareRequirements!;
        OtherInformation = string.IsNullOrWhiteSpace(game.OtherInformation)
            ? string.Empty
            : game.OtherInformation!;
        ShowHardware = HardwareRequirements.Length > 0;
        ShowOther = OtherInformation.Length > 0;
        ShowFeaturedBadge = game.FeaturedP;

        Badges.Clear();
        if (game.FeaturedP)
        {
            Badges.Add("Wyróżnione");
        }

        if (!string.IsNullOrWhiteSpace(game.GenreName))
        {
            Badges.Add(game.GenreName);
        }

        if (!string.IsNullOrWhiteSpace(game.EngineName))
        {
            Badges.Add(game.EngineName);
        }

        Badges.Add("Sklep SGS");
        foreach (var platform in game.Builds
                     .Select(b => FormatPlatform(b.Platform, b.Architecture))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Badges.Add(platform);
        }

        PlatformsText = game.Builds.Count == 0
            ? "Brak buildów"
            : string.Join(" · ", game.Builds
                .Select(b => FormatPlatform(b.Platform, b.Architecture))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        MediaThumbs.Clear();
        if (!string.IsNullOrWhiteSpace(game.CoverUrl))
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Okładka", Accent = "#3A5A80", ImageUrl = game.CoverUrl });
        }

        if (!string.IsNullOrWhiteSpace(game.IntroUrl))
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Intro", Accent = "#5A3A50", ImageUrl = game.IntroUrl });
        }

        var shotIndex = 1;
        foreach (var url in game.ScreenshotUrls)
        {
            MediaThumbs.Add(new MediaThumbItem
            {
                Label = $"Screen {shotIndex++}",
                Accent = "#2D5A45",
                ImageUrl = url
            });
        }

        if (MediaThumbs.Count == 0)
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Hero", Accent = "#3A5A80" });
            MediaThumbs.Add(new MediaThumbItem { Label = "Gameplay", Accent = "#2D5A45" });
            MediaThumbs.Add(new MediaThumbItem { Label = "Świat", Accent = "#5A3A50" });
            MediaThumbs.Add(new MediaThumbItem { Label = "UI", Accent = "#4A4A2D" });
        }

        var preferredPlatform = DetectCurrentPlatform();
        Builds.Clear();
        foreach (var build in game.Builds)
        {
            var row = new BuildRowItem
            {
                BuildId = build.Id,
                Platform = build.Platform,
                Architecture = build.Architecture,
                Version = build.CurrentVersion,
                ExeName = build.ExeName,
                PlatformText = FormatPlatform(build.Platform, build.Architecture),
                MetaText = build.ExeName + (build.IsDefault ? " · domyślny" : ""),
                IsPreferred = build.Platform == preferredPlatform
            };
            row.Versions.Add(VersionOptionItem.FromCurrent(build.CurrentVersion));
            row.SelectedVersion = row.Versions[0];
            Builds.Add(row);
        }

        ShowBuilds = Builds.Count > 0;
        SelectedTab = "Details";
    }

    private async Task EnrichBuildHistoryAsync(int gameId)
    {
        if (_api is null || Builds.Count == 0)
        {
            return;
        }

        foreach (var row in Builds)
        {
            var versions = await _api.GetBuildVersionsAsync(gameId, row.BuildId);
            if (versions.Count == 0)
            {
                continue;
            }

            row.Versions.Clear();
            foreach (var version in versions.OrderByDescending(v => v.IsCurrent).ThenByDescending(v => v.PublishedAtUtc))
            {
                row.Versions.Add(VersionOptionItem.FromDto(version));
            }

            row.SelectedVersion = row.Versions.FirstOrDefault(v => v.IsCurrent) ?? row.Versions.FirstOrDefault();
            row.HasVersionPicker = row.Versions.Count > 0;
        }
    }

    private void UpdateActionState(GameDto game)
    {
        var preferred = GameListItem.PickPreferredBuild(game.Builds);
        CanAdd = !InLibrary && preferred is not null;
        PrimaryActionLabel = InLibrary
            ? "W bibliotece"
            : preferred is null
                ? "Brak builda"
                : "Dodaj do biblioteki";
        ShowLibraryActions = InLibrary;
    }

    private async Task RefreshLibraryStateAsync(int gameId)
    {
        _libraryItem = null;
        ShowLibraryActions = false;
        CanLaunch = false;
        CanInstall = false;
        CanUninstall = false;

        if (_scopes is null)
        {
            InLibrary = false;
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            _libraryItem = await library.GetByGameIdAsync(gameId);
            InLibrary = _libraryItem is not null;
            if (_libraryItem is not null)
            {
                ApplyLibraryActions(_libraryItem);
            }
        }
        catch
        {
            InLibrary = false;
        }
    }

    private void ApplyLibraryActions(GameLibraryItem item)
    {
        ShowLibraryActions = true;
        CanLaunch = item.InstallState == InstallState.Installed
            && !string.IsNullOrWhiteSpace(item.ExecutablePath);
        CanInstall = item.InstallState is InstallState.NotInstalled or InstallState.Failed;
        CanUninstall = item.InstallState == InstallState.Installed;
    }

    private async Task<bool> IsInLibraryAsync(int gameId)
    {
        await RefreshLibraryStateAsync(gameId);
        return InLibrary;
    }

    private static GamePlatform DetectCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return GamePlatform.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return GamePlatform.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return GamePlatform.MacOS;
        }

        return GamePlatform.Windows;
    }

    private static string FormatPlatform(GamePlatform platform, string? architecture)
    {
        var name = platform switch
        {
            GamePlatform.Windows => "Windows",
            GamePlatform.Linux => "Linux",
            GamePlatform.MacOS => "macOS",
            _ => platform.ToString()
        };

        return string.IsNullOrWhiteSpace(architecture) ? name : $"{name} ({architecture})";
    }
}

public sealed class MediaThumbItem
{
    public required string Label { get; init; }
    public required string Accent { get; init; }
    public string? ImageUrl { get; init; }
}

public partial class BuildRowItem : ObservableObject
{
    public required int BuildId { get; init; }
    public required GamePlatform Platform { get; init; }
    public string? Architecture { get; init; }
    public required string Version { get; init; }
    public required string ExeName { get; init; }
    public required string PlatformText { get; init; }
    public string MetaText { get; set; } = string.Empty;
    public bool IsPreferred { get; init; }

    public ObservableCollection<VersionOptionItem> Versions { get; } = [];

    [ObservableProperty]
    private VersionOptionItem? _selectedVersion;

    [ObservableProperty]
    private bool _hasVersionPicker;
}

public sealed class VersionOptionItem
{
    public required string Version { get; init; }
    public required string DisplayText { get; init; }
    public bool IsCurrent { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }

    public static VersionOptionItem FromCurrent(string version) => new()
    {
        Version = version,
        DisplayText = $"v{version} (najnowsza)",
        IsCurrent = true,
        PublishedAtUtc = DateTimeOffset.UtcNow
    };

    public static VersionOptionItem FromDto(GameBuildVersionDto dto) => new()
    {
        Version = dto.Version,
        DisplayText = dto.IsCurrent
            ? $"v{dto.Version} (najnowsza)"
            : $"v{dto.Version} · {dto.PublishedAtUtc.LocalDateTime:g}",
        IsCurrent = dto.IsCurrent,
        PublishedAtUtc = dto.PublishedAtUtc
    };
}
