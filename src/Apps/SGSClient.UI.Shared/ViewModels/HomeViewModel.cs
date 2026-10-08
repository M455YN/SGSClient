using System.Collections.ObjectModel;
using System.Globalization;
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

public partial class HomeViewModel : ObservableObject
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private readonly IAuthSession? _auth;
    private readonly ICatalogCacheStore? _catalogCache;
    private readonly string _apiBaseUrl;
    private Action<string>? _navigate;

    public HomeViewModel()
        : this(null)
    {
    }

    public HomeViewModel(
        ISgsApiService? api,
        IServiceScopeFactory? scopes = null,
        IAuthSession? auth = null,
        IConfiguration? configuration = null,
        ICatalogCacheStore? catalogCache = null)
    {
        _api = api;
        _scopes = scopes;
        _auth = auth;
        _catalogCache = catalogCache;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        RefreshGreeting();
    }

    public void SetNavigateHandler(Action<string> navigate) => _navigate = navigate;

    public ObservableCollection<HomeGameCard> ContinuePlaying { get; } = [];
    public ObservableCollection<HomeGameCard> ReadyToInstall { get; } = [];
    public ObservableCollection<HomeGameCard> Featured { get; } = [];
    public ObservableCollection<HomeGameCard> Catalog { get; } = [];
    public ObservableCollection<HomeCategoryItem> Categories { get; } = [];
    public ObservableCollection<FeaturedSlideDot> FeaturedDots { get; } = [];

    private List<HomeGameCard> _slides = [];
    private int _slideIndex;

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Dzień dobry";

    [ObservableProperty]
    public partial string DateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial HomeGameCard? Hero { get; set; }

    [ObservableProperty]
    public partial bool HasHero { get; set; }

    [ObservableProperty]
    public partial bool HasContinue { get; set; }

    [ObservableProperty]
    public partial bool HasReadyToInstall { get; set; }

    [ObservableProperty]
    public partial bool HasMultipleSlides { get; set; }

    [ObservableProperty]
    public partial bool HasFeatured { get; set; }

    [ObservableProperty]
    public partial bool HasCatalog { get; set; }

    [ObservableProperty]
    public partial bool HasCategories { get; set; }

    [ObservableProperty]
    public partial string LibraryCountText { get; set; } = "0";

    [ObservableProperty]
    public partial string InstalledCountText { get; set; } = "0";

    [ObservableProperty]
    public partial string CatalogCountText { get; set; } = "0";

    [ObservableProperty]
    public partial bool ShowEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    /// <summary>Creator-only shortcuts stay hidden for regular accounts.</summary>
    [ObservableProperty]
    public partial bool CanPublish { get; set; } = true;

    public event Action<string>? NavigateRequested;
    public event Action<int>? OpenGameRequested;

    [RelayCommand]
    private void OpenStore() => RequestNavigate("Games");

    [RelayCommand]
    private void OpenLibrary() => RequestNavigate("Library");

    [RelayCommand]
    private void OpenPublish() => RequestNavigate("Studio");

    [RelayCommand]
    private void OpenFriends() => RequestNavigate("Friends");

    [RelayCommand]
    private void OpenCategory(HomeCategoryItem? item)
    {
        if (item is null)
        {
            RequestNavigate("Games");
            return;
        }

        RequestNavigate($"Games:genre:{item.Name}");
    }

    [RelayCommand]
    private void OpenGame(HomeGameCard? item)
    {
        if (item is null)
        {
            return;
        }

        OpenGameRequested?.Invoke(item.Id);
        _navigate?.Invoke($"Game:{item.Id}");
    }

    private void RequestNavigate(string tag)
    {
        _navigate?.Invoke(tag);
        NavigateRequested?.Invoke(tag);
    }

    [RelayCommand]
    private void OpenHero() => OpenGame(Hero);

    [RelayCommand]
    private void NextSlide() => ShowSlide(_slideIndex + 1);

    [RelayCommand]
    private void PreviousSlide() => ShowSlide(_slideIndex - 1);

    [RelayCommand]
    private void SelectSlide(int index) => ShowSlide(index);

    public async Task EnsureLoadedAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        RefreshGreeting();
        try
        {
            IReadOnlyList<GameDto> games = [];
            HasError = false;
            StatusText = string.Empty;

            if (_api is not null)
            {
                try
                {
                    games = await _api.GetGamesAsync();
                }
                catch (Exception ex)
                {
                    if (_catalogCache is not null)
                    {
                        var cached = await _catalogCache.LoadCatalogAsync();
                        if (cached.SavedAtUtc is not null)
                        {
                            games = cached.Games;
                            StatusText = $"Offline — cache katalogu z {cached.SavedAtUtc:g}";
                        }
                        else
                        {
                            HasError = true;
                            StatusText = $"Nie udało się pobrać katalogu: {ex.Message}";
                        }
                    }
                    else
                    {
                        HasError = true;
                        StatusText = $"Nie udało się pobrać katalogu: {ex.Message}";
                    }
                }
            }
            else
            {
                HasError = true;
                StatusText = "Brak klienta API. Sprawdź Api:BaseUrl w appsettings.";
            }

            var library = await LoadLibraryAsync();
            await SyncLibraryArtworkAsync(library, games);
            ApplyCatalog(games);
            ApplyLibrary(library);
            CatalogCountText = games.Count.ToString(CultureInfo.InvariantCulture);
            ShowEmpty = !HasHero && !HasContinue && !HasFeatured && !HasCatalog && !HasError;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshGreeting()
    {
        var now = DateTime.Now;
        var date = now.ToString("dddd, d MMMM", Polish);
        DateText = string.IsNullOrEmpty(date)
            ? string.Empty
            : char.ToUpper(date[0], Polish) + date[1..];

        var hello = now.Hour switch
        {
            >= 5 and < 18 => "Dzień dobry",
            _ => "Dobry wieczór"
        };

        var name = _auth?.IsLoggedIn == true && !string.IsNullOrWhiteSpace(_auth.CurrentUser?.DisplayName)
            ? _auth.CurrentUser!.DisplayName.Trim()
            : null;
        Greeting = string.IsNullOrWhiteSpace(name) ? hello : $"{hello}, {name}";
        CanPublish = _auth?.IsAvailable != true || _auth.CurrentUser?.CanPublish == true;
    }

    private void ApplyCatalog(IReadOnlyList<GameDto> games)
    {
        Featured.Clear();
        Catalog.Clear();

        var featured = games
            .Where(g => g.FeaturedP)
            .OrderBy(g => g.FeaturedOrder ?? int.MaxValue)
            .ThenBy(g => g.Title)
            .ToList();
        var spotlight = featured.Count > 0
            ? featured
            : games.Take(6).ToList();
        _slides = spotlight
            .Take(8)
            .Select(game => HomeGameCard.FromDto(game, _apiBaseUrl, 160, landscape: true))
            .ToList();
        HasHero = _slides.Count > 0;
        ShowSlide(0);

        var spotlightIds = _slides.Select(s => s.Id).ToHashSet();
        foreach (var game in featured.Where(g => !spotlightIds.Contains(g.Id)).Take(8))
        {
            Featured.Add(HomeGameCard.FromDto(game, _apiBaseUrl, 90, landscape: true));
        }

        HasFeatured = Featured.Count > 0;

        foreach (var game in games
                     .OrderByDescending(g => g.CreatedAtUtc)
                     .Where(g => g.Id != Hero?.Id)
                     .Take(12))
        {
            Catalog.Add(HomeGameCard.FromDto(game, _apiBaseUrl, 70, landscape: true));
        }

        HasCatalog = Catalog.Count > 0;

        Categories.Clear();
        foreach (var group in games
                     .Where(g => !string.IsNullOrWhiteSpace(g.GenreName))
                     .GroupBy(g => g.GenreName!, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(g => g.Count())
                     .Take(8))
        {
            Categories.Add(new HomeCategoryItem
            {
                Name = group.Key,
                CountText = $"{group.Count()} gier"
            });
        }

        HasCategories = Categories.Count > 0;
    }

    private void ShowSlide(int index)
    {
        if (_slides.Count == 0)
        {
            Hero = null;
            HasMultipleSlides = false;
            FeaturedDots.Clear();
            return;
        }

        _slideIndex = (index % _slides.Count + _slides.Count) % _slides.Count;
        Hero = _slides[_slideIndex];
        HasMultipleSlides = _slides.Count > 1;

        FeaturedDots.Clear();
        for (var i = 0; i < _slides.Count; i++)
        {
            FeaturedDots.Add(new FeaturedSlideDot { Index = i, IsActive = i == _slideIndex });
        }
    }

    private async Task SyncLibraryArtworkAsync(IReadOnlyList<GameLibraryItem> library, IReadOnlyList<GameDto> games)
    {
        if (_scopes is null || games.Count == 0)
        {
            return;
        }

        var byId = games.ToDictionary(g => g.Id);
        List<GameLibraryItem>? dirty = null;
        foreach (var item in library)
        {
            if (!byId.TryGetValue(item.GameId, out var game))
            {
                continue;
            }

            if (item.ApplyStoreArtwork(game.CoverUrl, game.LogoUrl, game.HeaderUrl))
            {
                dirty ??= [];
                dirty.Add(item);
            }
        }

        if (dirty is null)
        {
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            foreach (var item in dirty)
            {
                await store.UpsertAsync(item);
            }
        }
        catch
        {
            // Artwork still applies in memory for this session.
        }
    }

    private void ApplyLibrary(IReadOnlyList<GameLibraryItem> library)
    {
        ContinuePlaying.Clear();
        foreach (var item in library
                     .Where(x => x.InstallState == InstallState.Installed || x.LastPlayedUtc is not null)
                     .OrderByDescending(x => x.LastPlayedUtc ?? DateTimeOffset.MinValue)
                     .ThenBy(x => x.Title)
                     .Take(10))
        {
            ContinuePlaying.Add(HomeGameCard.FromLibrary(item, _apiBaseUrl));
        }

        HasContinue = ContinuePlaying.Count > 0;

        ReadyToInstall.Clear();
        foreach (var item in library
                     .Where(x => x.InstallState != InstallState.Installed && x.LastPlayedUtc is null)
                     .OrderBy(x => x.Title)
                     .Take(10))
        {
            ReadyToInstall.Add(HomeGameCard.FromLibrary(item, _apiBaseUrl));
        }

        HasReadyToInstall = ReadyToInstall.Count > 0;

        LibraryCountText = library.Count.ToString(CultureInfo.InvariantCulture);
        InstalledCountText = library
            .Count(x => x.InstallState == InstallState.Installed)
            .ToString(CultureInfo.InvariantCulture);
    }

    private async Task<IReadOnlyList<GameLibraryItem>> LoadLibraryAsync()
    {
        if (_scopes is null)
        {
            return [];
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var items = await store.GetAllAsync();
            var playtime = scope.ServiceProvider.GetService<IPlaytimeStore>();
            if (playtime is not null)
            {
                await playtime.SyncFromServerAsync();
                await playtime.ApplyToLibraryAsync(items);
            }

            return items;
        }
        catch
        {
            return [];
        }
    }
}

public sealed class HomeCategoryItem
{
    public required string Name { get; init; }
    public required string CountText { get; init; }
    public string Initial => Name[..1].ToUpperInvariant();
}

public sealed class FeaturedSlideDot
{
    public required int Index { get; init; }
    public bool IsActive { get; init; }
}

public sealed class HomeGameCard
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string TileLabel { get; init; }
    public string Blurb { get; init; } = string.Empty;
    public string? CoverImageUrl { get; init; }
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImageUrl);
    public string Caption { get; init; } = string.Empty;
    public bool ShowFeaturedBadge { get; init; }
    public string ReleaseLine { get; init; } = string.Empty;
    public string BadgeText { get; init; } = string.Empty;
    public string? Shot1 { get; init; }
    public string? Shot2 { get; init; }
    public string? Shot3 { get; init; }
    public string? Shot4 { get; init; }
    public int ShotCount { get; init; }
    public bool HasShots => ShotCount > 0;
    public bool ShotLayout1 => ShotCount == 1;
    public bool ShotLayout2 => ShotCount == 2;
    public bool ShotLayout3 => ShotCount == 3;
    public bool ShotLayout4 => ShotCount >= 4;

    public static HomeGameCard FromDto(GameDto dto, string apiBaseUrl, int blurbLength, bool landscape = false)
    {
        var shots = dto.ScreenshotUrls
            .Select(url => ResolveCover(url, apiBaseUrl))
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Take(4)
            .ToList();

        return new HomeGameCard
        {
            Id = dto.Id,
            Title = dto.Title,
            TileLabel = MakeTile(dto.Title, dto.Symbol),
            Blurb = Truncate(dto.Description, blurbLength),
            CoverImageUrl = ResolveCover(landscape ? dto.LandscapeUrl : dto.ThumbnailUrl, apiBaseUrl),
            Caption = string.IsNullOrWhiteSpace(dto.GenreName) ? "Sklep" : dto.GenreName!,
            ShowFeaturedBadge = dto.FeaturedP,
            ReleaseLine = ReleaseLineFor(dto.ReleaseStatus),
            BadgeText = string.IsNullOrWhiteSpace(dto.GenreName) ? "Wyróżnione" : dto.GenreName!,
            ShotCount = shots.Count,
            Shot1 = shots.ElementAtOrDefault(0),
            Shot2 = shots.ElementAtOrDefault(1),
            Shot3 = shots.ElementAtOrDefault(2),
            Shot4 = shots.ElementAtOrDefault(3)
        };
    }

    private static string ReleaseLineFor(GameReleaseStatus status) => status switch
    {
        GameReleaseStatus.EarlyAccess => "Wczesny dostęp już dostępny",
        GameReleaseStatus.Announced => "Zapowiedź",
        GameReleaseStatus.Demo => "Demo już dostępne",
        _ => "Już dostępna"
    };

    public static HomeGameCard FromLibrary(GameLibraryItem item, string apiBaseUrl) => new()
    {
        Id = item.GameId,
        Title = item.Title,
        TileLabel = MakeTile(item.Title, item.Symbol),
        CoverImageUrl = ResolveLibraryCover(item, apiBaseUrl),
        Caption = FormatContinueCaption(item.LastPlayedUtc, item.TotalPlayTimeSeconds),
        ShowFeaturedBadge = false
    };

    private static string MakeTile(string title, string? symbol) =>
        string.IsNullOrWhiteSpace(symbol)
            ? title[..Math.Min(2, title.Length)].ToUpperInvariant()
            : symbol[..Math.Min(3, symbol.Length)].ToUpperInvariant();

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Zobacz w sklepie";
        }

        var value = text.Trim();
        return value.Length <= max ? value : value[..Math.Max(0, max - 1)].TrimEnd() + "…";
    }

    private static string? ResolveCover(string? coverUrl, string apiBaseUrl) =>
        InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, coverUrl);

    private static string? ResolveLibraryCover(GameLibraryItem item, string apiBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(item.CustomCoverPath) && File.Exists(item.CustomCoverPath))
        {
            return new Uri(item.CustomCoverPath).AbsoluteUri;
        }

        return ResolveCover(item.CoverUrl, apiBaseUrl);
    }

    private static string FormatContinueCaption(DateTimeOffset? utc, long totalSeconds)
    {
        var when = FormatLastPlayed(utc);
        return totalSeconds > 0
            ? $"{when} · {PlaytimeFormat.ToPolish(totalSeconds)}"
            : when;
    }

    private static string FormatLastPlayed(DateTimeOffset? utc)
    {
        if (utc is null)
        {
            return "W bibliotece";
        }

        var days = (DateTime.Now.Date - utc.Value.LocalDateTime.Date).TotalDays;
        return days switch
        {
            < 1 => "Dzisiaj",
            < 2 => "Wczoraj",
            < 7 => $"{(int)days} dni temu",
            _ => utc.Value.LocalDateTime.ToString("d", CultureInfo.GetCultureInfo("pl-PL"))
        };
    }
}
