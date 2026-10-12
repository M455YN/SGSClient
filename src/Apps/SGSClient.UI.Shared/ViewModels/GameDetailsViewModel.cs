using SGSClient.Core.Localization;
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
        StatusText = Loc.T("Ładowanie…");

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
    public partial string PrimaryActionLabel { get; set; } = Loc.T("Dodaj do biblioteki");

    [ObservableProperty]
    public partial bool ShowHardware { get; set; }

    [ObservableProperty]
    public partial bool ShowOther { get; set; }

    [ObservableProperty]
    public partial bool ShowBuilds { get; set; }

    [ObservableProperty]
    public partial bool FromLibrary { get; set; }

    [ObservableProperty]
    public partial string BackLabel { get; set; } = Loc.T("← Sklep");

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
    public partial string WishlistLabel { get; set; } = Loc.T("Lista życzeń");

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

    [ObservableProperty] public partial string ReviewsSummaryText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool CanWriteReview { get; set; }
    [ObservableProperty] public partial bool ReviewRecommended { get; set; } = true;
    [ObservableProperty] public partial string ReviewDraftText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasMyReview { get; set; }
    [ObservableProperty] public partial string ReviewStatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedReviewSort { get; set; } = Loc.T("Najnowsze");
    [ObservableProperty] public partial string SelectedReviewFilter { get; set; } = Loc.T("Wszystkie");

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
            "Library" => Loc.T("← Biblioteka"),
            "Wishlist" => Loc.T("← Lista życzeń"),
            _ => Loc.T("← Sklep")
        };
        ContextLabel = ReturnTag switch
        {
            "Library" => "BIBLIOTEKA",
            "Wishlist" => Loc.T("LISTA ŻYCZEŃ"),
            _ => "SGS STORE"
        };
        if (_api is null)
        {
            StatusText = Loc.T("Brak klienta API.");
            HasContent = false;
            return;
        }

        IsBusy = true;
        HasContent = false;
        StatusText = Loc.T("Ładowanie szczegółów…");
        try
        {
            var game = await _api.GetGameAsync(gameId);
            if (game is null)
            {
                StatusText = Loc.T("Nie znaleziono gry.");
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
            StatusText = Loc.F("Błąd: {0}", ex.Message);
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
            ? string.Empty
            : game.Description.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() is { Length: > 0 } first
                ? first + "."
                : game.Description!;
        Description = string.IsNullOrWhiteSpace(game.Description) ? Loc.T("Brak opisu.") : game.Description!;
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

        Badges.Add(Loc.T("Sklep SGS"));
        foreach (var platform in game.Builds
                     .Select(b => FormatPlatform(b.Platform, b.Architecture))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Badges.Add(platform);
        }

        PlatformsText = game.Builds.Count == 0
            ? Loc.T("Brak buildów")
            : string.Join(" · ", game.Builds
                .Select(b => FormatPlatform(b.Platform, b.Architecture))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        MediaThumbs.Clear();
        if (!string.IsNullOrWhiteSpace(IntroMediaUrl))
        {
            MediaThumbs.Add(new MediaThumbItem
            {
                Label = "Intro ▶",
                ImageUrl = IntroMediaUrl,
                IsImage = false,
                IsVideo = true
            });
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
            MediaThumbs.Add(new MediaThumbItem { Label = Loc.T("Świat") });
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
                MetaText = build.ExeName + (build.IsDefault ? Loc.T(" · domyślny") : ""),
                IsPreferred = build.Platform == preferredPlatform,
                PreferredLabel = build.Platform == preferredPlatform ? Loc.T("Twoja platforma") : string.Empty
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
            ? Loc.T("W bibliotece")
            : preferred is null
                ? Loc.T("Brak builda")
                : Loc.T("Dodaj do biblioteki");
        ShowLibraryActions = InLibrary;
        ShowStoreActions = !InLibrary;
        CanToggleWishlist = !InLibrary;
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
