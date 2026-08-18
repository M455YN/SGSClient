using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Core.Playtime;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class GameDetailsViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private readonly string _apiBaseUrl;
    private string? _coverUrl;
    private GameLibraryItem? _libraryItem;
    private Action<GameLibraryItem>? _openProperties;

    public GameDetailsViewModel(
        ISgsApiService? api,
        IServiceScopeFactory? scopes,
        IConfiguration? configuration = null)
    {
        _api = api;
        _scopes = scopes;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = "Ładowanie…";
    }

    public void SetOpenPropertiesHandler(Action<GameLibraryItem> open) => _openProperties = open;

    public ObservableCollection<string> Badges { get; } = [];
    public ObservableCollection<MediaThumbItem> MediaThumbs { get; } = [];
    public ObservableCollection<BuildRowItem> Builds { get; } = [];

    [ObservableProperty]
    public partial int GameId { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Symbol { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TileLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CoverImageUrl { get; set; }

    [ObservableProperty]
    public partial bool HasCover { get; set; }

    [ObservableProperty]
    public partial string? IntroMediaUrl { get; set; }

    [ObservableProperty]
    public partial bool HasIntro { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewCounterText))]
    [NotifyPropertyChangedFor(nameof(CanPreviewPrevious))]
    [NotifyPropertyChangedFor(nameof(CanPreviewNext))]
    [NotifyPropertyChangedFor(nameof(PreviewHintText))]
    public partial bool IsMediaPreviewOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewCounterText))]
    [NotifyPropertyChangedFor(nameof(CanPreviewPrevious))]
    [NotifyPropertyChangedFor(nameof(CanPreviewNext))]
    [NotifyPropertyChangedFor(nameof(PreviewHintText))]
    public partial int MediaPreviewIndex { get; set; }

    [ObservableProperty]
    public partial string? MediaPreviewUrl { get; set; }

    [ObservableProperty]
    public partial string MediaPreviewLabel { get; set; } = string.Empty;

    public string PreviewCounterText
    {
        get
        {
            var count = PreviewableThumbs.Count;
            return count == 0 ? string.Empty : $"{MediaPreviewIndex + 1} / {count}";
        }
    }

    public bool CanPreviewPrevious => IsMediaPreviewOpen && MediaPreviewIndex > 0;

    public bool CanPreviewNext =>
        IsMediaPreviewOpen && MediaPreviewIndex < PreviewableThumbs.Count - 1;

    public string PreviewHintText =>
        PreviewableThumbs.Count > 1
            ? "Kliknij strzałkę lub tło, żeby zobaczyć kolejne zdjęcie"
            : "Kliknij tło lub ✕, żeby zamknąć";

    private IReadOnlyList<MediaThumbItem> PreviewableThumbs =>
        MediaThumbs.Where(t => t.HasMedia).ToList();

    [ObservableProperty]
    public partial string Tagline { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HardwareRequirements { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OtherInformation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlatformsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlaytimeText { get; set; } = string.Empty;

    public bool HasPlaytime => !string.IsNullOrWhiteSpace(PlaytimeText);

    partial void OnPlaytimeTextChanged(string value) => OnPropertyChanged(nameof(HasPlaytime));

    [ObservableProperty]
    public partial string SelectedTab { get; set; } = "Details";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasContent { get; set; }

    [ObservableProperty]
    public partial bool InLibrary { get; set; }

    [ObservableProperty]
    public partial bool CanAdd { get; set; }

    [ObservableProperty]
    public partial string PrimaryActionLabel { get; set; } = "Dodaj do biblioteki";

    [ObservableProperty]
    public partial bool ShowHardware { get; set; }

    [ObservableProperty]
    public partial bool ShowOther { get; set; }

    [ObservableProperty]
    public partial bool ShowBuilds { get; set; }

    [ObservableProperty]
    public partial bool FromLibrary { get; set; }

    [ObservableProperty]
    public partial string BackLabel { get; set; } = "← Sklep";

    [ObservableProperty]
    public partial string ContextLabel { get; set; } = "SGS STORE";

    [ObservableProperty]
    public partial bool CanLaunch { get; set; }

    [ObservableProperty]
    public partial bool IsDlc { get; set; }

    [ObservableProperty]
    public partial bool CanInstall { get; set; }

    [ObservableProperty]
    public partial bool CanUninstall { get; set; }

    [ObservableProperty]
    public partial bool ShowLibraryActions { get; set; }

    [ObservableProperty]
    public partial bool ShowStoreActions { get; set; } = true;

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial double InstallProgressFraction { get; set; }

    [ObservableProperty]
    public partial string InstallProgressText { get; set; } = string.Empty;

    public string ReturnTag { get; private set; } = "Games";

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
        CloseMediaPreview();
        GameId = gameId;
        FromLibrary = fromLibrary;
        ReturnTag = fromLibrary ? "Library" : "Games";
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
            await SyncAndRefreshPlaytimeAsync(game.Id);
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
    private void SelectTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            SelectedTab = tab;
        }
    }

    [RelayCommand]
    private void OpenMediaPreview(MediaThumbItem? thumb)
    {
        if (thumb is null || !thumb.HasMedia)
        {
            return;
        }

        var items = PreviewableThumbs;
        var index = items.ToList().FindIndex(t =>
            string.Equals(t.ImageUrl, thumb.ImageUrl, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        ShowPreviewAt(index);
    }

    [RelayCommand]
    private void OpenCoverPreview()
    {
        var items = PreviewableThumbs;
        if (items.Count == 0)
        {
            return;
        }

        ShowPreviewAt(0);
    }

    [RelayCommand]
    private void CloseMediaPreview() => IsMediaPreviewOpen = false;

    [RelayCommand]
    private void PreviewPrevious()
    {
        if (!CanPreviewPrevious)
        {
            return;
        }

        ShowPreviewAt(MediaPreviewIndex - 1);
    }

    [RelayCommand]
    private void PreviewNext()
    {
        if (!CanPreviewNext)
        {
            return;
        }

        ShowPreviewAt(MediaPreviewIndex + 1);
    }

    private void ShowPreviewAt(int index)
    {
        var items = PreviewableThumbs;
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        var item = items[index];
        MediaPreviewIndex = index;
        MediaPreviewUrl = item.ImageUrl;
        MediaPreviewLabel = item.Label;
        IsMediaPreviewOpen = true;
        OnPropertyChanged(nameof(PreviewCounterText));
        OnPropertyChanged(nameof(CanPreviewPrevious));
        OnPropertyChanged(nameof(CanPreviewNext));
        OnPropertyChanged(nameof(PreviewHintText));
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
            var result = await launcher.LaunchAsync(new GameLaunchRequest
            {
                GameId = _libraryItem.GameId,
                Title = Title,
                ExecutablePath = _libraryItem.ExecutablePath
            });
            StatusText = result.Message;
            await RefreshPlaytimeAsync(_libraryItem.GameId);
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
        IsDlc = game.Kind == GameContentKind.Dlc;
        _coverUrl = game.CoverUrl;
        CoverImageUrl = ResolveMediaUrl(game.CoverUrl);
        HasCover = !string.IsNullOrWhiteSpace(CoverImageUrl);
        IntroMediaUrl = ResolveMediaUrl(game.IntroUrl);
        HasIntro = !string.IsNullOrWhiteSpace(IntroMediaUrl);
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
        HardwareRequirements = game.HardwareRequirements ?? string.Empty;
        OtherInformation = game.OtherInformation ?? string.Empty;
        ShowHardware = HardwareRequirements.Length > 0;
        ShowOther = OtherInformation.Length > 0;

        Badges.Clear();
        foreach (var badge in GameStoreLabels.CatalogBadges(game))
        {
            Badges.Add(badge);
        }

        if (!string.IsNullOrWhiteSpace(game.StudioName))
        {
            Badges.Insert(0, game.StudioName);
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
        if (!string.IsNullOrWhiteSpace(CoverImageUrl))
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Okładka", ImageUrl = CoverImageUrl, IsImage = true });
        }

        var shotIndex = 1;
        foreach (var url in game.ScreenshotUrls)
        {
            var absolute = ResolveMediaUrl(url);
            if (string.IsNullOrWhiteSpace(absolute))
            {
                continue;
            }

            MediaThumbs.Add(new MediaThumbItem { Label = $"Screen {shotIndex++}", ImageUrl = absolute, IsImage = true });
        }

        if (MediaThumbs.Count == 0)
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Hero" });
            MediaThumbs.Add(new MediaThumbItem { Label = "Gameplay" });
            MediaThumbs.Add(new MediaThumbItem { Label = "Świat" });
            MediaThumbs.Add(new MediaThumbItem { Label = "UI" });
        }

        var preferredPlatform = GamePlatform.Windows;
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
                IsPreferred = build.Platform == preferredPlatform,
                PreferredLabel = build.Platform == preferredPlatform ? "Twoja platforma" : string.Empty
            };
            row.Versions.Add(VersionOptionItem.FromCurrent(build.CurrentVersion));
            row.SelectedVersion = row.Versions[0];
            row.HasVersionPicker = true;
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
        ShowStoreActions = !InLibrary;
    }

    private async Task RefreshLibraryStateAsync(int gameId)
    {
        _libraryItem = null;
        ShowLibraryActions = false;
        ShowStoreActions = true;
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

            await RefreshPlaytimeAsync(gameId);
        }
        catch
        {
            InLibrary = false;
        }
    }

    private async Task SyncAndRefreshPlaytimeAsync(int gameId)
    {
        if (_scopes is null)
        {
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var store = scope.ServiceProvider.GetService<IPlaytimeStore>();
            if (store is null)
            {
                return;
            }

            await store.SyncFromServerAsync();
        }
        catch
        {
        }

        await RefreshPlaytimeAsync(gameId);
    }

    private async Task RefreshPlaytimeAsync(int gameId)
    {
        if (_scopes is null)
        {
            PlaytimeText = string.Empty;
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var store = scope.ServiceProvider.GetService<IPlaytimeStore>();
            var record = store is null ? null : await store.GetAsync(gameId);
            PlaytimeText = record is { TotalSeconds: > 0 }
                ? PlaytimeFormat.ToPolishTotal(record.TotalSeconds)
                : string.Empty;
        }
        catch
        {
            PlaytimeText = string.Empty;
        }
    }

    private void ApplyLibraryActions(GameLibraryItem item)
    {
        ShowLibraryActions = true;
        ShowStoreActions = false;
        CanLaunch = !IsDlc
            && item.InstallState == InstallState.Installed
            && !string.IsNullOrWhiteSpace(item.ExecutablePath);
        CanInstall = item.InstallState is InstallState.NotInstalled or InstallState.Failed;
        CanUninstall = item.InstallState == InstallState.Installed;
    }

    private async Task<bool> IsInLibraryAsync(int gameId)
    {
        await RefreshLibraryStateAsync(gameId);
        return InLibrary;
    }

    private string? ResolveMediaUrl(string? relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            return null;
        }

        var resolved = InstallPathHelper.ResolveDownloadUrl(_apiBaseUrl, relativeOrAbsolute);
        return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
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
    public string? ImageUrl { get; init; }
    public bool IsImage { get; init; }
    public bool HasMedia => !string.IsNullOrWhiteSpace(ImageUrl);
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
    public string PreferredLabel { get; init; } = string.Empty;

    public ObservableCollection<VersionOptionItem> Versions { get; } = [];

    [ObservableProperty]
    public partial VersionOptionItem? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial bool HasVersionPicker { get; set; }
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
