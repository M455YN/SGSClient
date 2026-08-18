using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
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

public partial class HomeViewModel : ViewModelBase
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
        IConfiguration? configuration = null,
        Action<string>? navigate = null)
    {
        _api = api;
        _scopes = scopes;
        _auth = auth;
        _navigate = navigate;
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
    private string _greeting = "Dzień dobry";

    [ObservableProperty]
    private string _dateText = string.Empty;

    [ObservableProperty]
    private HomeGameCard? _hero;

    [ObservableProperty]
    private bool _hasHero;

    [ObservableProperty]
    private bool _hasContinue;

    [ObservableProperty]
    private bool _hasReadyToInstall;

    [ObservableProperty]
    private bool _hasFeatured;

    [ObservableProperty]
    private bool _hasCatalog;

    [ObservableProperty]
    private bool _hasCategories;

    [ObservableProperty]
    private string _libraryCountText = "0";

    [ObservableProperty]
    private string _installedCountText = "0";

    [ObservableProperty]
    private string _catalogCountText = "0";

    /// <summary>Creator-only shortcuts stay hidden for regular accounts.</summary>
    [ObservableProperty]
    private bool _canPublish = true;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [RelayCommand]
    private void OpenStore() => _navigate?.Invoke("Games");

    [RelayCommand]
    private void OpenLibrary() => _navigate?.Invoke("Library");

    [RelayCommand]
    private void OpenPublish() => _navigate?.Invoke("Publish");

    [RelayCommand]
    private void OpenFriends() => _navigate?.Invoke("Friends");

    [RelayCommand]
    private void OpenCategory(HomeCategoryItem? item) => _navigate?.Invoke("Games");

    [RelayCommand]
    private void OpenGame(HomeGameCard? item)
    {
        if (item is null)
        {
            return;
        }

        _navigate?.Invoke($"Game:{item.Id}");
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

        var hello = now.Hour is >= 5 and < 18 ? "Dzień dobry" : "Dobry wieczór";
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
        Hero = heroDto is null ? null : HomeGameCard.FromDto(heroDto, _apiBaseUrl, 160);
        HasHero = Hero is not null;
        _ = Hero?.LoadCoverAsync();

        foreach (var game in featured.Where(g => g.Id != Hero?.Id).Take(8))
        {
            var card = HomeGameCard.FromDto(game, _apiBaseUrl, 90);
            Featured.Add(card);
            _ = card.LoadCoverAsync();
        }

        HasFeatured = Featured.Count > 0;

        foreach (var game in games
                     .OrderByDescending(g => g.CreatedAtUtc)
                     .Where(g => g.Id != Hero?.Id)
                     .Take(12))
        {
            var card = HomeGameCard.FromDto(game, _apiBaseUrl, 70);
            Catalog.Add(card);
            _ = card.LoadCoverAsync();
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
            var card = HomeGameCard.FromLibrary(item, _apiBaseUrl);
            ContinuePlaying.Add(card);
            _ = card.LoadCoverAsync();
        }

        HasContinue = ContinuePlaying.Count > 0;

        ReadyToInstall.Clear();
        foreach (var item in library
                     .Where(x => x.InstallState != InstallState.Installed && x.LastPlayedUtc is null)
                     .OrderBy(x => x.Title)
                     .Take(10))
        {
            var card = HomeGameCard.FromLibrary(item, _apiBaseUrl);
            ReadyToInstall.Add(card);
            _ = card.LoadCoverAsync();
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

public partial class HomeGameCard : ObservableObject
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string TileLabel { get; init; }
    public string Blurb { get; init; } = string.Empty;
    public string? CoverImageUrl { get; init; }
    public string Caption { get; init; } = string.Empty;
    public bool ShowFeaturedBadge { get; init; }

    [ObservableProperty]
    private Bitmap? _coverBitmap;

    [ObservableProperty]
    private bool _hasCover;

    public static HomeGameCard FromDto(GameDto dto, string apiBaseUrl, int blurbLength) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        TileLabel = MakeTile(dto.Title, dto.Symbol),
        Blurb = Truncate(dto.Description, blurbLength),
        CoverImageUrl = string.IsNullOrWhiteSpace(dto.CoverUrl)
            ? null
            : InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, dto.CoverUrl),
        Caption = string.IsNullOrWhiteSpace(dto.GenreName) ? "Sklep" : dto.GenreName!,
        ShowFeaturedBadge = dto.FeaturedP
    };

    public static HomeGameCard FromLibrary(GameLibraryItem item, string apiBaseUrl)
    {
        string? cover = null;
        if (!string.IsNullOrWhiteSpace(item.CustomCoverPath) && File.Exists(item.CustomCoverPath))
        {
            cover = item.CustomCoverPath;
        }
        else if (!string.IsNullOrWhiteSpace(item.CoverUrl))
        {
            cover = InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, item.CoverUrl);
        }

        return new HomeGameCard
        {
            Id = item.GameId,
            Title = item.Title,
            TileLabel = MakeTile(item.Title, item.Symbol),
            CoverImageUrl = cover,
            Caption = FormatContinueCaption(item.LastPlayedUtc, item.TotalPlayTimeSeconds)
        };
    }

    public async Task LoadCoverAsync()
    {
        var url = CoverImageUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            CoverBitmap = null;
            HasCover = false;
            return;
        }

        try
        {
            if (File.Exists(url))
            {
                CoverBitmap = new Bitmap(url);
                HasCover = true;
                return;
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            await using var stream = await http.GetStreamAsync(url);
            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            CoverBitmap = new Bitmap(memory);
            HasCover = true;
        }
        catch
        {
            CoverBitmap = null;
            HasCover = false;
        }
    }

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
