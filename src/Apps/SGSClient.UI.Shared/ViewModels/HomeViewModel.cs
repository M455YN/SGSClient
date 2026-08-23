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
        IConfiguration? configuration = null)
    {
        _api = api;
        _scopes = scopes;
        _auth = auth;
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
    private void OpenCategory(HomeCategoryItem? item) => RequestNavigate("Games");

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
                    HasError = true;
                    StatusText = $"Nie udało się pobrać katalogu: {ex.Message}";
                }
            }
            else
            {
                HasError = true;
                StatusText = "Brak klienta API. Sprawdź Api:BaseUrl w appsettings.";
            }

            var library = await LoadLibraryAsync();
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
        var heroDto = featured.FirstOrDefault() ?? games.FirstOrDefault();
        Hero = heroDto is null ? null : HomeGameCard.FromDto(heroDto, _apiBaseUrl, 160, landscape: true);
        HasHero = Hero is not null;

        foreach (var game in featured.Where(g => g.Id != Hero?.Id).Take(8))
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

    public static HomeGameCard FromDto(GameDto dto, string apiBaseUrl, int blurbLength, bool landscape = false) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        TileLabel = MakeTile(dto.Title, dto.Symbol),
        Blurb = Truncate(dto.Description, blurbLength),
        CoverImageUrl = ResolveCover(landscape ? dto.LandscapeUrl : dto.ThumbnailUrl, apiBaseUrl),
        Caption = string.IsNullOrWhiteSpace(dto.GenreName) ? "Sklep" : dto.GenreName!,
        ShowFeaturedBadge = dto.FeaturedP
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
