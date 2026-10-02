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

namespace SGSClient.UI.ViewModels;

public partial class GameDetailsViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private readonly IInstallQueueService? _queue;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl;
    private string? _coverUrl;
    private string? _logoUrl;
    private GameLibraryItem? _libraryItem;
    private Action? _close;
    private Action? _openAccount;
    private Action<GameLibraryItem>? _openProperties;

    public GameDetailsViewModel()
        : this(null, null)
    {
    }

    public GameDetailsViewModel(
        ISgsApiService? api,
        IServiceScopeFactory? scopes,
        IConfiguration? configuration = null,
        IInstallQueueService? queue = null,
        IAuthSession? auth = null)
    {
        _api = api;
        _scopes = scopes;
        _queue = queue;
        _auth = auth;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = "Ładowanie…";

        if (_queue is not null)
        {
            _queue.Changed += (_, _) => SyncInstallQueueState();
            _queue.ProgressChanged += OnQueueProgress;
        }
    }

    public void SetCloseHandler(Action close) => _close = close;
    public void SetOpenAccountHandler(Action openAccount) => _openAccount = openAccount;
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
    public partial bool ShowFeaturedBadge { get; set; }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial bool InWishlist { get; set; }

    [ObservableProperty]
    public partial string WishlistLabel { get; set; } = "Lista życzeń";

    [ObservableProperty]
    public partial bool CanToggleWishlist { get; set; } = true;

    [ObservableProperty]
    public partial double InstallProgressFraction { get; set; }

    [ObservableProperty]
    public partial string InstallProgressText { get; set; } = string.Empty;

    public string ReturnTag { get; private set; } = "Games";

    public bool ShowDetailsTab => SelectedTab == "Details";
    public bool ShowReviewsTab => SelectedTab == "Reviews";
    public bool ShowMoreTab => SelectedTab == "More";

    public bool ReviewsAvailable => true;

    public ObservableCollection<GameReviewItem> Reviews { get; } = [];

    public IReadOnlyList<string> ReviewSortOptions { get; } = ["Najnowsze", "Najbardziej pomocne"];
    public IReadOnlyList<string> ReviewFilterOptions { get; } = ["Wszystkie", "Pozytywne", "Negatywne"];

    [ObservableProperty] public partial string ReviewsSummaryText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool CanWriteReview { get; set; }
    [ObservableProperty] public partial bool ReviewRecommended { get; set; } = true;
    [ObservableProperty] public partial string ReviewDraftText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasMyReview { get; set; }
    [ObservableProperty] public partial string ReviewStatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedReviewSort { get; set; } = "Najnowsze";
    [ObservableProperty] public partial string SelectedReviewFilter { get; set; } = "Wszystkie";

    partial void OnSelectedReviewSortChanged(string value) => _ = LoadReviewsAsync();
    partial void OnSelectedReviewFilterChanged(string value) => _ = LoadReviewsAsync();

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(ShowDetailsTab));
        OnPropertyChanged(nameof(ShowReviewsTab));
        OnPropertyChanged(nameof(ShowMoreTab));
    }

    public async Task LoadAsync(int gameId, bool fromLibrary = false, string? returnTag = null)
    {
        CloseMediaPreview();
        GameId = gameId;
        FromLibrary = fromLibrary;
        ReturnTag = string.IsNullOrWhiteSpace(returnTag)
            ? (fromLibrary ? "Library" : "Games")
            : returnTag.Trim();
        BackLabel = ReturnTag switch
        {
            "Library" => "← Biblioteka",
            "Wishlist" => "← Lista życzeń",
            _ => "← Sklep"
        };
        ContextLabel = ReturnTag switch
        {
            "Library" => "BIBLIOTEKA",
            "Wishlist" => "LISTA ŻYCZEŃ",
            _ => "SGS STORE"
        };
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
            await RefreshWishlistStateAsync(game.Id);
            UpdateActionState(game);
            HasContent = true;
            StatusText = string.Empty;
            _ = LoadReviewsAsync();
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
            if (string.Equals(tab, "Reviews", StringComparison.OrdinalIgnoreCase))
            {
                _ = LoadReviewsAsync();
            }
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

        if (_auth is { IsAvailable: true } && !_auth.IsLoggedIn)
        {
            StatusText = "Zaloguj się, żeby dodać grę do biblioteki.";
            _openAccount?.Invoke();
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
            if (_api is not null)
            {
                var claimed = await _api.ClaimGameAsync(GameId);
                if (claimed is null)
                {
                    StatusText = "Nie udało się dodać gry (zaloguj się lub sprawdź uprawnienia).";
                    return;
                }
            }

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
                LogoUrl = _logoUrl,
                ExecutablePath = null,
                InstallPath = null
            });

            InLibrary = true;
            InWishlist = false;
            WishlistLabel = "Lista życzeń";
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
    private async Task ToggleWishlistAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        if (_auth is { IsAvailable: true } && !_auth.IsLoggedIn)
        {
            StatusText = "Zaloguj się, żeby zarządzać listą życzeń.";
            _openAccount?.Invoke();
            return;
        }

        try
        {
            if (InWishlist)
            {
                if (await _api.RemoveFromWishlistAsync(GameId))
                {
                    InWishlist = false;
                    WishlistLabel = "Lista życzeń";
                    StatusText = "Usunięto z listy życzeń.";
                }
            }
            else
            {
                if (await _api.AddToWishlistAsync(GameId) is not null)
                {
                    InWishlist = true;
                    WishlistLabel = "Na liście życzeń";
                    StatusText = "Dodano do listy życzeń.";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Lista życzeń: {ex.Message}";
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

        if (_queue is not null)
        {
            IsInstalling = true;
            StatusText = "Dodano do kolejki instalacji…";
            await _queue.EnqueueInstallAsync(GameId, _libraryItem.TargetVersion);
            SyncInstallQueueState();
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
    private void CancelInstall()
    {
        if (_queue is null || GameId <= 0 || !IsInstalling)
        {
            return;
        }

        _queue.Cancel(GameId);
        IsInstalling = false;
        InstallProgressFraction = 0;
        InstallProgressText = string.Empty;
        StatusText = "Anulowano instalację.";
        SyncInstallQueueState();
        _ = RefreshLibraryStateAsync(GameId);
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
        _coverUrl = game.ThumbnailUrl;
        _logoUrl = game.LogoUrl;
        CoverImageUrl = ResolveMediaUrl(game.LandscapeUrl);
        HasCover = !string.IsNullOrWhiteSpace(CoverImageUrl);
        ShowFeaturedBadge = game.FeaturedP;
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
        var thumbUrl = ResolveMediaUrl(game.CoverUrl);
        var headerUrl = ResolveMediaUrl(game.HeaderUrl);
        var logoUrl = ResolveMediaUrl(game.LogoUrl);
        if (!string.IsNullOrWhiteSpace(thumbUrl))
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Miniaturka", ImageUrl = thumbUrl, IsImage = true });
        }

        if (!string.IsNullOrWhiteSpace(headerUrl)
            && !string.Equals(headerUrl, thumbUrl, StringComparison.OrdinalIgnoreCase))
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Okładka pozioma", ImageUrl = headerUrl, IsImage = true });
        }

        if (!string.IsNullOrWhiteSpace(logoUrl)
            && !string.Equals(logoUrl, thumbUrl, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(logoUrl, headerUrl, StringComparison.OrdinalIgnoreCase))
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Logo", ImageUrl = logoUrl, IsImage = true });
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

        var preferredPlatform = GameListItem.DetectCurrentPlatform();
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
        CanToggleWishlist = !InLibrary;
    }

    private async Task RefreshWishlistStateAsync(int gameId)
    {
        InWishlist = false;
        WishlistLabel = "Lista życzeń";
        if (_api is null)
        {
            return;
        }

        try
        {
            var wish = await _api.GetWishlistAsync();
            InWishlist = wish.Any(g => g.Id == gameId);
            WishlistLabel = InWishlist ? "Na liście życzeń" : "Lista życzeń";
        }
        catch
        {
            // ignore
        }
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

    private void OnQueueProgress(object? sender, InstallQueueProgressEventArgs e)
    {
        if (e.GameId != GameId)
        {
            return;
        }

        InstallProgressFraction = e.Progress.Fraction;
        InstallProgressText = e.Progress.Message ?? string.Empty;
        IsInstalling = e.State is InstallState.Queued or InstallState.Downloading
            or InstallState.Installing or InstallState.Updating;
        if (!IsInstalling)
        {
            _ = RefreshLibraryStateAsync(GameId);
        }
    }

    private void SyncInstallQueueState()
    {
        if (_queue is null || GameId <= 0)
        {
            return;
        }

        var active = _queue.IsQueuedOrActive(GameId);
        IsInstalling = active;
        if (!active)
        {
            InstallProgressFraction = 0;
            if (string.Equals(InstallProgressText, "Dodano do kolejki instalacji…", StringComparison.Ordinal))
            {
                InstallProgressText = string.Empty;
            }

            var entry = _queue.Entries.FirstOrDefault(e => e.GameId == GameId);
            if (!string.IsNullOrWhiteSpace(entry?.Error))
            {
                StatusText = entry.Error!;
            }

            _ = RefreshLibraryStateAsync(GameId);
        }
    }

    private async Task LoadReviewsAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        try
        {
            var sort = SelectedReviewSort.Contains("pomocne", StringComparison.OrdinalIgnoreCase)
                ? "helpful"
                : "recent";
            bool? recommended = SelectedReviewFilter switch
            {
                "Pozytywne" => true,
                "Negatywne" => false,
                _ => null
            };

            var summary = await _api.GetGameReviewsAsync(GameId, sort, recommended);
            Reviews.Clear();
            if (summary is null)
            {
                ReviewsSummaryText = "Brak recenzji.";
                CanWriteReview = false;
                HasMyReview = false;
                return;
            }

            CanWriteReview = summary.CanReview;
            HasMyReview = summary.MyReview is not null;
            ReviewsSummaryText = summary.TotalCount == 0
                ? "Brak recenzji — bądź pierwszy."
                : $"{summary.RecommendedPercent:0.#}% z {summary.TotalCount} recenzji poleca tę grę";

            if (summary.MyReview is not null)
            {
                ReviewRecommended = summary.MyReview.Recommended;
                ReviewDraftText = summary.MyReview.Text;
            }

            foreach (var r in summary.Reviews)
            {
                Reviews.Add(GameReviewItem.FromDto(r));
            }
        }
        catch (Exception ex)
        {
            ReviewStatusText = ex.Message;
        }
    }

    [RelayCommand]
    private async Task VoteHelpfulAsync(GameReviewItem? item)
    {
        if (item is null || _api is null || item.IsMine)
        {
            return;
        }

        if (!await _api.VoteGameReviewAsync(item.Id, helpful: true))
        {
            ReviewStatusText = "Nie udało się zagłosować.";
            return;
        }

        await LoadReviewsAsync();
    }

    [RelayCommand]
    private async Task ToggleCommentsAsync(GameReviewItem? item)
    {
        if (item is null || _api is null)
        {
            return;
        }

        if (item.CommentsExpanded)
        {
            item.CommentsExpanded = false;
            return;
        }

        item.CommentsExpanded = true;
        var comments = await _api.GetGameReviewCommentsAsync(item.Id);
        item.Comments.Clear();
        foreach (var c in comments)
        {
            item.Comments.Add(GameReviewCommentItem.FromDto(c));
        }
    }

    [RelayCommand]
    private async Task AddCommentAsync(GameReviewItem? item)
    {
        if (item is null || _api is null || string.IsNullOrWhiteSpace(item.CommentDraft))
        {
            return;
        }

        var (comment, error) = await _api.AddGameReviewCommentAsync(item.Id, item.CommentDraft);
        if (error is not null || comment is null)
        {
            ReviewStatusText = error ?? "Nie udało się dodać komentarza.";
            return;
        }

        item.CommentDraft = string.Empty;
        item.CommentCount++;
        item.Comments.Add(GameReviewCommentItem.FromDto(comment));
        item.CommentsExpanded = true;
    }

    [RelayCommand]
    private async Task DeleteCommentAsync(GameReviewCommentItem? comment)
    {
        if (comment is null || _api is null)
        {
            return;
        }

        if (!await _api.DeleteGameReviewCommentAsync(comment.Id))
        {
            ReviewStatusText = "Nie udało się usunąć komentarza.";
            return;
        }

        var parent = Reviews.FirstOrDefault(r => r.Id == comment.ReviewId);
        if (parent is not null)
        {
            parent.Comments.Remove(comment);
            parent.CommentCount = Math.Max(0, parent.CommentCount - 1);
        }
    }

    [RelayCommand]
    private async Task SubmitReviewAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        ReviewStatusText = "Zapisywanie…";
        var (review, error) = await _api.UpsertGameReviewAsync(
            GameId,
            new UpsertGameReviewRequest
            {
                Recommended = ReviewRecommended,
                Text = ReviewDraftText
            });
        if (error is not null || review is null)
        {
            ReviewStatusText = error ?? "Nie udało się zapisać recenzji.";
            return;
        }

        ReviewStatusText = "Zapisano recenzję.";
        await LoadReviewsAsync();
    }

    [RelayCommand]
    private async Task DeleteReviewAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        if (!await _api.DeleteGameReviewAsync(GameId))
        {
            ReviewStatusText = "Nie udało się usunąć recenzji.";
            return;
        }

        ReviewDraftText = string.Empty;
        ReviewRecommended = true;
        ReviewStatusText = "Usunięto recenzję.";
        await LoadReviewsAsync();
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

    private string? ResolveMediaUrl(string? relativeOrAbsolute) =>
        InstallPathHelper.TryResolveDownloadUrl(_apiBaseUrl, relativeOrAbsolute);

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

public partial class GameReviewItem : ObservableObject
{
    public long Id { get; init; }
    public required string AuthorDisplayName { get; init; }
    public string? AuthorPlayerTag { get; init; }
    public bool Recommended { get; init; }
    public required string Text { get; init; }
    public string VerdictLabel => Recommended ? "Poleca" : "Nie poleca";
    public string MetaText { get; init; } = string.Empty;
    public bool IsMine { get; init; }
    public int HelpfulCount { get; set; }
    public bool? MyHelpfulVote { get; set; }
    public int CommentCount { get; set; }
    public bool CanVote => !IsMine;
    public string HelpfulButtonText => MyHelpfulVote == true
        ? $"Pomocne ✓ ({HelpfulCount})"
        : $"Pomocne ({HelpfulCount})";
    public string CommentsToggleText => CommentsExpanded
        ? "Ukryj komentarze"
        : $"Komentarze ({CommentCount})";

    public ObservableCollection<GameReviewCommentItem> Comments { get; } = [];

    [ObservableProperty] public partial bool CommentsExpanded { get; set; }
    [ObservableProperty] public partial string CommentDraft { get; set; } = string.Empty;

    partial void OnCommentsExpandedChanged(bool value) => OnPropertyChanged(nameof(CommentsToggleText));

    public static GameReviewItem FromDto(GameReviewDto dto)
    {
        var play = dto.PlaytimeSecondsAtReview is > 0
            ? $" · {PlaytimeFormat.ToPolishTotal(dto.PlaytimeSecondsAtReview.Value)}"
            : string.Empty;
        var tag = string.IsNullOrWhiteSpace(dto.AuthorPlayerTag) ? "" : $" @{dto.AuthorPlayerTag}";
        return new GameReviewItem
        {
            Id = dto.Id,
            AuthorDisplayName = dto.AuthorDisplayName,
            AuthorPlayerTag = dto.AuthorPlayerTag,
            Recommended = dto.Recommended,
            Text = dto.Text,
            IsMine = dto.IsMine,
            HelpfulCount = dto.HelpfulCount,
            MyHelpfulVote = dto.MyHelpfulVote,
            CommentCount = dto.CommentCount,
            MetaText = $"{dto.AuthorDisplayName}{tag} · {dto.UpdatedAtUtc.LocalDateTime:g}{play}"
        };
    }
}

public sealed class GameReviewCommentItem
{
    public long Id { get; init; }
    public long ReviewId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public required string Text { get; init; }
    public string MetaText { get; init; } = string.Empty;
    public bool IsMine { get; init; }

    public static GameReviewCommentItem FromDto(GameReviewCommentDto dto)
    {
        var tag = string.IsNullOrWhiteSpace(dto.AuthorPlayerTag) ? "" : $" @{dto.AuthorPlayerTag}";
        return new GameReviewCommentItem
        {
            Id = dto.Id,
            ReviewId = dto.ReviewId,
            AuthorDisplayName = dto.AuthorDisplayName,
            Text = dto.Text,
            IsMine = dto.IsMine,
            MetaText = $"{dto.AuthorDisplayName}{tag} · {dto.CreatedAtUtc.LocalDateTime:g}"
        };
    }
}
