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

public partial class GamesViewModel : ViewModelBase
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private readonly string _apiBaseUrl;
    private Action<int>? _openDetails;
    private bool _loaded;

    public GamesViewModel()
    {
        StatusText = "API not configured";
        _apiBaseUrl = new ApiClientSettings().BaseUrl;
    }

    public GamesViewModel(
        ISgsApiService? api,
        IServiceScopeFactory? scopes = null,
        Action<int>? openDetails = null,
        IConfiguration? configuration = null)
    {
        _api = api;
        _scopes = scopes;
        _openDetails = openDetails;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = api is null ? "API not registered" : "Ładowanie sklepu…";
    }

    public void SetOpenDetailsHandler(Action<int> openDetails) => _openDetails = openDetails;

    public string Title { get; } = "Sklep";

    public ObservableCollection<GameListItem> Games { get; } = [];

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasGames;

    [ObservableProperty]
    private bool _showEmpty;

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
            var games = await _api.GetGamesAsync();
            var owned = await GetOwnedGameIdsAsync();

            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(GameListItem.FromDto(game, owned.Contains(game.Id), _apiBaseUrl));
            }

            _loaded = true;
            HasGames = Games.Count > 0;
            ShowEmpty = !HasGames;
            StatusText = HasGames
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

    public Task EnsureLoadedAsync() => _loaded && Games.Count > 0 ? RefreshOwnedFlagsAsync() : LoadAsync();

    [RelayCommand]
    private void OpenDetails(GameListItem? item)
    {
        if (item is null)
        {
            return;
        }

        _openDetails?.Invoke(item.Id);
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

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            await library.UpsertAsync(new GameLibraryItem
            {
                GameId = item.Id,
                BuildId = item.BuildId,
                Symbol = item.Symbol,
                Title = item.Title,
                Platform = item.Platform,
                Architecture = item.Architecture,
                InstallState = InstallState.NotInstalled,
                InstalledVersion = null,
                TargetVersion = item.Version,
                CoverUrl = item.CoverUrl,
                ExecutablePath = null,
                InstallPath = null
            });

            item.InLibrary = true;
            StatusText = $"Dodano do biblioteki: {item.Title}";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się dodać gry: {ex.Message}";
        }
    }

    private async Task RefreshOwnedFlagsAsync()
    {
        var owned = await GetOwnedGameIdsAsync();
        foreach (var game in Games)
        {
            game.InLibrary = owned.Contains(game.Id);
        }
    }

    private async Task<HashSet<int>> GetOwnedGameIdsAsync()
    {
        if (_scopes is null)
        {
            return [];
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var items = await library.GetAllAsync();
            return items.Select(x => x.GameId).ToHashSet();
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
    public string? CoverImageUrl { get; init; }

    [ObservableProperty]
    private Avalonia.Media.Imaging.Bitmap? _coverBitmap;

    [ObservableProperty]
    private bool _hasCover;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd))]
    [NotifyPropertyChangedFor(nameof(ActionLabel))]
    private bool _inLibrary;

    public bool CanAdd => !InLibrary && BuildId > 0;
    public string ActionLabel => InLibrary ? "W bibliotece" : (BuildId > 0 ? "Dodaj do biblioteki" : "Brak builda");

    public static GameListItem FromDto(GameDto dto, bool inLibrary, string apiBaseUrl)
    {
        var build = PickPreferredBuild(dto.Builds);
        var platforms = dto.Builds
            .Select(b => FormatPlatform(b.Platform, b.Architecture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var coverImageUrl = string.IsNullOrWhiteSpace(dto.CoverUrl)
            ? null
            : InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, dto.CoverUrl);

        var item = new GameListItem
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
            CoverUrl = dto.CoverUrl,
            CoverImageUrl = string.IsNullOrWhiteSpace(coverImageUrl) ? null : coverImageUrl,
            InLibrary = inLibrary
        };
        _ = item.LoadCoverAsync();
        return item;
    }

    private async Task LoadCoverAsync()
    {
        if (string.IsNullOrWhiteSpace(CoverImageUrl))
        {
            CoverBitmap = null;
            HasCover = false;
            return;
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            await using var stream = await http.GetStreamAsync(CoverImageUrl);
            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;
            CoverBitmap = new Avalonia.Media.Imaging.Bitmap(memory);
            HasCover = true;
        }
        catch
        {
            CoverBitmap = null;
            HasCover = false;
        }
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
