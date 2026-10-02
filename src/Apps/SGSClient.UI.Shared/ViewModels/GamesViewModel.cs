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

namespace SGSClient.UI.ViewModels;

public partial class GamesViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private readonly IAuthSession? _auth;
    private readonly ICatalogCacheStore? _catalogCache;
    private readonly string _apiBaseUrl;
    private Action<int>? _openDetails;
    private Action? _openAccount;
    private bool _loaded;
    private CancellationTokenSource? _reloadCts;

    public GamesViewModel()
        : this(null)
    {
    }

    public GamesViewModel(
        ISgsApiService? api,
        IServiceScopeFactory? scopes = null,
        IConfiguration? configuration = null,
        IAuthSession? auth = null,
        ICatalogCacheStore? catalogCache = null)
    {
        _api = api;
        _scopes = scopes;
        _auth = auth;
        _catalogCache = catalogCache;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = api is null ? "API not registered" : "Ładowanie sklepu…";
        SortOptions = ["Wyróżnione", "Tytuł", "Najnowsze"];
        SelectedSort = SortOptions[0];
    }

    public void SetOpenDetailsHandler(Action<int> openDetails) => _openDetails = openDetails;
    public void SetOpenAccountHandler(Action openAccount) => _openAccount = openAccount;

    public string Title { get; } = "Sklep";

    public ObservableCollection<GameListItem> Games { get; } = [];
    public IReadOnlyList<string> SortOptions { get; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasGames { get; set; }

    [ObservableProperty]
    public partial bool ShowEmpty { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GenreFilter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedSort { get; set; } = "Wyróżnione";

    [ObservableProperty]
    public partial string WishlistSectionTitle { get; set; } = "Lista życzeń";

    public ObservableCollection<GameListItem> Wishlist { get; } = [];

    [ObservableProperty]
    public partial bool HasWishlist { get; set; }

    partial void OnSearchTextChanged(string value) => _ = DebouncedReloadAsync();
    partial void OnGenreFilterChanged(string value) => _ = DebouncedReloadAsync();
    partial void OnSelectedSortChanged(string value) => _ = DebouncedReloadAsync();

    public void ApplyGenreFilter(string? genre)
    {
        GenreFilter = genre?.Trim() ?? string.Empty;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_api is null)
        {
            StatusText = "Brak klienta API. Sprawdź Api:BaseUrl w appsettings.";
            ShowEmpty = true;
            HasGames = false;
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusText = "Ładowanie sklepu…";
            var query = BuildQuery();
            IReadOnlyList<GameDto> games;
            var fromCache = false;
            DateTimeOffset? cacheAt = null;
            try
            {
                games = await _api.GetGamesAsync(query);
            }
            catch (Exception)
            {
                if (_catalogCache is null)
                {
                    throw;
                }

                var cached = await _catalogCache.LoadCatalogAsync();
                games = FilterCachedGames(cached.Games, query);
                cacheAt = cached.SavedAtUtc;
                fromCache = cacheAt is not null;
                if (!fromCache)
                {
                    throw;
                }
            }

            var owned = await GetOwnedGameIdsAsync();
            HashSet<int> wishIds;
            try
            {
                wishIds = await GetWishlistIdsAsync();
            }
            catch
            {
                wishIds = [];
            }

            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(GameListItem.FromDto(game, owned.Contains(game.Id), _apiBaseUrl, wishIds.Contains(game.Id)));
            }

            await LoadWishlistSectionAsync(owned);

            _loaded = true;
            HasGames = Games.Count > 0;
            ShowEmpty = !HasGames;
            StatusText = fromCache
                ? HasGames
                    ? $"Offline — cache sklepu z {cacheAt:g} ({Games.Count})"
                    : "Offline — brak cache sklepu."
                : HasGames
                    ? $"{Games.Count} gier w sklepie"
                    : "Brak gier. Uruchom Api i upewnij się, że baza ma seed.";
        }
        catch (Exception ex)
        {
            HasGames = false;
            ShowEmpty = true;
            StatusText = $"Nie udało się pobrać sklepu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IReadOnlyList<GameDto> FilterCachedGames(IReadOnlyList<GameDto> games, GameCatalogQuery? query)
    {
        if (query is null)
        {
            return games;
        }

        IEnumerable<GameDto> q = games;
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var term = query.Q.Trim();
            q = q.Where(g =>
                g.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (g.Symbol?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            var genre = query.Genre.Trim();
            q = q.Where(g =>
                (g.GenreSymbol?.Equals(genre, StringComparison.OrdinalIgnoreCase) ?? false)
                || (g.GenreName?.Equals(genre, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return q.ToList();
    }

    public Task EnsureLoadedAsync() => _loaded && Games.Count > 0 ? RefreshOwnedFlagsAsync() : LoadAsync();

    [RelayCommand]
    private void ClearGenreFilter() => GenreFilter = string.Empty;

    [RelayCommand]
    private void OpenDetails(GameListItem? item)
    {
        if (item is not null)
        {
            _openDetails?.Invoke(item.Id);
        }
    }

    [RelayCommand]
    private async Task AddToLibraryAsync(GameListItem? item)
    {
        if (item is null || !item.CanAdd || _scopes is null)
        {
            return;
        }

        if (item.BuildId <= 0)
        {
            StatusText = $"Brak builda dla tej platformy: {item.Title}";
            return;
        }

        if (_auth is { IsAvailable: true } && !_auth.IsLoggedIn)
        {
            StatusText = "Zaloguj się, żeby dodać grę do biblioteki.";
            _openAccount?.Invoke();
            return;
        }

        try
        {
            GameDto? claimed = null;
            if (_api is not null)
            {
                claimed = await _api.ClaimGameAsync(item.Id);
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
                GameId = item.Id,
                BuildId = item.BuildId,
                Symbol = item.Symbol,
                Title = claimed?.Title ?? item.Title,
                Platform = item.Platform,
                Architecture = item.Architecture,
                InstallState = InstallState.NotInstalled,
                InstalledVersion = null,
                TargetVersion = item.Version,
                CoverUrl = item.CoverUrl,
                LogoUrl = item.LogoUrl,
                ExecutablePath = null,
                InstallPath = null
            });

            item.InLibrary = true;
            item.InWishlist = false;
            StatusText = $"Dodano do biblioteki: {item.Title}";
            await LoadWishlistSectionAsync(await GetOwnedGameIdsAsync());
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się dodać gry: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleWishlistAsync(GameListItem? item)
    {
        if (item is null || _api is null)
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
            if (item.InWishlist)
            {
                if (await _api.RemoveFromWishlistAsync(item.Id))
                {
                    item.InWishlist = false;
                    StatusText = $"Usunięto z listy życzeń: {item.Title}";
                }
            }
            else
            {
                if (await _api.AddToWishlistAsync(item.Id) is not null)
                {
                    item.InWishlist = true;
                    StatusText = $"Dodano do listy życzeń: {item.Title}";
                }
            }

            await LoadWishlistSectionAsync(await GetOwnedGameIdsAsync());
        }
        catch (Exception ex)
        {
            StatusText = $"Lista życzeń: {ex.Message}";
        }
    }

    private async Task DebouncedReloadAsync()
    {
        _reloadCts?.Cancel();
        _reloadCts = new CancellationTokenSource();
        var token = _reloadCts.Token;
        try
        {
            await Task.Delay(350, token);
            if (!token.IsCancellationRequested)
            {
                await LoadAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // superseded
        }
    }

    private GameCatalogQuery BuildQuery()
    {
        var sort = SelectedSort switch
        {
            "Tytuł" => "title",
            "Najnowsze" => "newest",
            _ => "featured"
        };

        return new GameCatalogQuery
        {
            Q = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            Genre = string.IsNullOrWhiteSpace(GenreFilter) ? null : GenreFilter.Trim(),
            Sort = sort
        };
    }

    private async Task LoadWishlistSectionAsync(HashSet<int> owned)
    {
        Wishlist.Clear();
        if (_api is null || _auth is { IsAvailable: true, IsLoggedIn: false })
        {
            HasWishlist = false;
            return;
        }

        try
        {
            var wish = await _api.GetWishlistAsync();
            foreach (var game in wish)
            {
                Wishlist.Add(GameListItem.FromDto(game, owned.Contains(game.Id), _apiBaseUrl, inWishlist: true));
            }
        }
        catch
        {
            // ignore
        }

        HasWishlist = Wishlist.Count > 0;
    }

    private async Task RefreshOwnedFlagsAsync()
    {
        var owned = await GetOwnedGameIdsAsync();
        var wish = await GetWishlistIdsAsync();
        foreach (var game in Games)
        {
            game.InLibrary = owned.Contains(game.Id);
            game.InWishlist = wish.Contains(game.Id);
        }

        await LoadWishlistSectionAsync(owned);
    }

    private async Task<HashSet<int>> GetOwnedGameIdsAsync()
    {
        var ids = new HashSet<int>();
        if (_api is not null && (_auth is null || !_auth.IsAvailable || _auth.IsLoggedIn))
        {
            try
            {
                foreach (var g in await _api.GetMyLibraryAsync())
                {
                    ids.Add(g.Id);
                }
            }
            catch
            {
                // fall through to local
            }
        }

        if (_scopes is null)
        {
            return ids;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            foreach (var item in await library.GetAllAsync())
            {
                ids.Add(item.GameId);
            }
        }
        catch
        {
            // ignore
        }

        return ids;
    }

    private async Task<HashSet<int>> GetWishlistIdsAsync()
    {
        if (_api is null)
        {
            return [];
        }

        try
        {
            return (await _api.GetWishlistAsync()).Select(g => g.Id).ToHashSet();
        }
        catch
        {
            return [];
        }
    }
}

public partial class GameListItem : ObservableObject
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool Featured { get; init; }
    public string TileLabel { get; init; } = string.Empty;
    public string PlatformsText { get; init; } = string.Empty;
    public string FeaturedBadge => Featured ? "Wyróżnione" : string.Empty;
    public bool ShowFeaturedBadge => Featured;

    public int BuildId { get; init; }
    public GamePlatform Platform { get; init; }
    public string? Architecture { get; init; }
    public string? Version { get; init; }
    public string? CoverUrl { get; init; }
    public string? LogoUrl { get; init; }
    public string? CoverImageUrl { get; init; }
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImageUrl);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(ActionLabel))]
    public partial bool InLibrary { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WishlistLabel))]
    public partial bool InWishlist { get; set; }

    public bool CanAdd => !InLibrary && BuildId > 0;
    public string ActionLabel => InLibrary ? "W bibliotece" : (BuildId > 0 ? "Dodaj do biblioteki" : "Brak builda");
    public string WishlistLabel => InWishlist ? "Na liście życzeń" : "Lista życzeń";

    public static GameListItem FromDto(GameDto dto, bool inLibrary, string apiBaseUrl, bool inWishlist = false)
    {
        var build = PickPreferredBuild(dto.Builds);
        var platforms = dto.Builds
            .Select(b => FormatPlatform(b.Platform, b.Architecture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var coverImageUrl = ToImageSourceUrl(InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, dto.LandscapeUrl));

        return new GameListItem
        {
            Id = dto.Id,
            Title = dto.Title,
            Symbol = dto.Symbol,
            Description = string.IsNullOrWhiteSpace(dto.Description)
                ? "Brak opisu."
                : dto.Description!,
            Featured = dto.FeaturedP,
            TileLabel = string.IsNullOrWhiteSpace(dto.Symbol)
                ? dto.Title[..Math.Min(2, dto.Title.Length)].ToUpperInvariant()
                : dto.Symbol[..Math.Min(3, dto.Symbol.Length)].ToUpperInvariant(),
            PlatformsText = platforms.Length == 0 ? "Brak buildów" : string.Join(" · ", platforms),
            BuildId = build?.Id ?? 0,
            Platform = build?.Platform ?? DetectCurrentPlatform(),
            Architecture = build?.Architecture,
            Version = build?.CurrentVersion,
            CoverUrl = dto.ThumbnailUrl,
            LogoUrl = dto.LogoUrl,
            CoverImageUrl = coverImageUrl,
            InLibrary = inLibrary,
            InWishlist = inWishlist
        };
    }

    private static string? ToImageSourceUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https" or "file" ? uri.AbsoluteUri : null;
    }

    public static GameBuildDto? PickPreferredBuild(IReadOnlyList<GameBuildDto> builds)
    {
        if (builds.Count == 0)
        {
            return null;
        }

        var platform = DetectCurrentPlatform();
        return builds.FirstOrDefault(b => b.Platform == platform && b.IsDefault)
            ?? builds.FirstOrDefault(b => b.Platform == platform)
            ?? builds.FirstOrDefault(b => b.IsDefault)
            ?? builds[0];
    }

    public static GamePlatform DetectCurrentPlatform()
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
