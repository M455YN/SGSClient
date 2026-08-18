using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Core.Playtime;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly IGameLauncherService? _launcher;
    private readonly IGameInstallService? _installer;
    private readonly ILibraryDiscoveryService? _discovery;
    private readonly ISgsApiService? _api;
    private readonly string _apiBaseUrl;
    private Action<LibraryGameItem>? _openProperties;
    private Action<int>? _openDetails;

    public LibraryViewModel(
        IServiceScopeFactory? scopes,
        IGameLauncherService? launcher = null,
        IGameInstallService? installer = null,
        ILibraryDiscoveryService? discovery = null,
        ISgsApiService? api = null,
        IConfiguration? configuration = null)
    {
        _scopes = scopes;
        _launcher = launcher;
        _installer = installer;
        _discovery = discovery;
        _api = api;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = "Ładowanie biblioteki…";
    }

    public void SetOpenPropertiesHandler(Action<LibraryGameItem> open) => _openProperties = open;
    public void SetOpenDetailsHandler(Action<int> open) => _openDetails = open;

    public string Title { get; } = "Biblioteka";

    public ObservableCollection<LibraryGameItem> Games { get; } = [];
    public ObservableCollection<LibraryGameItem> FilteredGames { get; } = [];
    public ObservableCollection<LibraryGameItem> RecentGames { get; } = [];
    public ObservableCollection<LibraryGameItem> AllGames { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasGames { get; set; }

    [ObservableProperty]
    public partial bool ShowEmpty { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial LibraryGameItem? SelectedGame { get; set; }

    [ObservableProperty]
    public partial bool IsHomeSelected { get; set; } = true;

    public bool ShowHome => HasGames;
    public string AllGamesHeader => $"Wszystkie gry ({AllGames.Count})";
    public bool ShowRecent => RecentGames.Count > 0;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnHasGamesChanged(bool value) => OnPropertyChanged(nameof(ShowHome));

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_scopes is null)
        {
            StatusText = "Brak magazynu lokalnego.";
            ShowEmpty = true;
            HasGames = false;
            return;
        }

        IsBusy = true;
        try
        {
            if (_discovery is not null)
            {
                using var scanScope = _scopes.CreateScope();
                var scanner = scanScope.ServiceProvider.GetRequiredService<ILibraryDiscoveryService>();
                await scanner.ScanAsync();
            }

            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var items = await library.GetAllAsync();
            var playtime = scope.ServiceProvider.GetService<IPlaytimeStore>();
            if (playtime is not null)
            {
                await playtime.SyncFromServerAsync();
                await playtime.ApplyToLibraryAsync(items);
            }

            Games.Clear();
            foreach (var item in items.OrderBy(x => x.Title))
            {
                Games.Add(LibraryGameItem.FromModel(item, _apiBaseUrl));
            }

            await EnrichCoversAsync(library);
            RebuildShelves();
            ApplyFilter();

            HasGames = Games.Count > 0;
            ShowEmpty = !HasGames;
            StatusText = HasGames
                ? $"{Games.Count} gier w bibliotece"
                : "Biblioteka jest pusta. Dodaj gry ze Sklepu.";

            if (SelectedGame is not null)
            {
                SelectedGame = Games.FirstOrDefault(g => g.GameId == SelectedGame.GameId);
                if (SelectedGame is null)
                {
                    IsHomeSelected = true;
                }
            }
        }
        catch (Exception ex)
        {
            HasGames = false;
            ShowEmpty = true;
            StatusText = $"Błąd biblioteki: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectHome()
    {
        IsHomeSelected = true;
        SelectedGame = null;
    }

    [RelayCommand]
    private void SelectGame(LibraryGameItem? item)
    {
        if (item is null)
        {
            return;
        }

        IsHomeSelected = false;
        SelectedGame = item;
        _openDetails?.Invoke(item.GameId);
    }

    [RelayCommand]
    private void OpenProperties(LibraryGameItem? item)
    {
        var target = item ?? SelectedGame;
        if (target is null)
        {
            return;
        }

        _openProperties?.Invoke(target);
    }

    [RelayCommand]
    private async Task InstallAsync(LibraryGameItem? item)
    {
        item ??= SelectedGame;
        if (item is null || _scopes is null || _installer is null || item.IsInstalling)
        {
            return;
        }

        item.IsInstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(item.GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            var targetVersion = model.TargetVersion;
            model.InstallState = InstallState.Downloading;
            await library.UpsertAsync(model);
            item.ApplyModel(model, _apiBaseUrl);

            var progress = new Progress<InstallProgress>(p =>
            {
                item.InstallProgressFraction = p.Fraction;
                item.InstallProgressText = p.Message ?? string.Empty;
                if (p.Phase is InstallPhase.Downloading)
                {
                    item.ApplyInstallState(InstallState.Downloading);
                }
                else if (p.Phase is InstallPhase.Installing)
                {
                    item.ApplyInstallState(InstallState.Installing);
                }
            });

            var result = await _installer.InstallAsync(model, version: targetVersion, progress: progress);
            await library.UpsertAsync(model);
            item.ApplyModel(model, _apiBaseUrl);
            RebuildShelves();

            StatusText = result.Success
                ? string.IsNullOrWhiteSpace(targetVersion)
                    ? $"Zainstalowano: {model.Title} (najnowsza)."
                    : $"Zainstalowano: {model.Title} (v{targetVersion})."
                : result.Errors.FirstOrDefault() ?? "Instalacja nie powiodła się.";
        }
        catch (Exception ex)
        {
            StatusText = $"Instalacja nie powiodła się: {ex.Message}";
        }
        finally
        {
            item.IsInstalling = false;
            item.InstallProgressFraction = 0;
            item.InstallProgressText = string.Empty;
        }
    }

    [RelayCommand]
    private async Task UninstallAsync(LibraryGameItem? item)
    {
        item ??= SelectedGame;
        if (item is null || _scopes is null || _installer is null || item.IsInstalling)
        {
            return;
        }

        item.IsInstalling = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var model = await library.GetByGameIdAsync(item.GameId);
            if (model is null)
            {
                StatusText = "Nie znaleziono gry w bibliotece.";
                return;
            }

            var result = await _installer.UninstallAsync(model);
            await library.UpsertAsync(model);
            item.ApplyModel(model, _apiBaseUrl);
            RebuildShelves();

            StatusText = result.Success
                ? $"Odinstalowano: {model.Title} (gra zostaje w bibliotece)."
                : result.Errors.FirstOrDefault() ?? "Odinstalowanie nie powiodło się.";
        }
        catch (Exception ex)
        {
            StatusText = $"Odinstalowanie nie powiodło się: {ex.Message}";
        }
        finally
        {
            item.IsInstalling = false;
        }
    }

    [RelayCommand]
    private async Task LaunchAsync(LibraryGameItem? item)
    {
        item ??= SelectedGame;
        if (item is null || _launcher is null || string.IsNullOrWhiteSpace(item.ExecutablePath))
        {
            StatusText = item is null
                ? "Brak gry."
                : "Gra nie jest zainstalowana. Kliknij Zainstaluj.";
            return;
        }

        try
        {
            var result = await _launcher.LaunchAsync(new GameLaunchRequest
            {
                GameId = item.GameId,
                Title = item.Title,
                ExecutablePath = item.ExecutablePath
            });
            StatusText = result.Message;
            if (_scopes is not null)
            {
                using var scope = _scopes.CreateScope();
                var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
                var existing = await library.GetByGameIdAsync(item.GameId);
                if (existing is not null)
                {
                    var playtime = scope.ServiceProvider.GetService<IPlaytimeStore>();
                    if (playtime is not null)
                    {
                        await playtime.ApplyToLibraryAsync([existing]);
                    }

                    item.ApplyModel(existing, _apiBaseUrl);
                    RebuildShelves();
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się uruchomić: {ex.Message}";
        }
    }

    private async Task EnrichCoversAsync(ILocalLibraryStore library)
    {
        if (_api is null)
        {
            return;
        }

        foreach (var row in Games.Where(g => string.IsNullOrWhiteSpace(g.CoverUrl)))
        {
            try
            {
                var game = await _api.GetGameAsync(row.GameId);
                if (string.IsNullOrWhiteSpace(game?.ThumbnailUrl))
                {
                    continue;
                }

                var model = await library.GetByGameIdAsync(row.GameId);
                if (model is null)
                {
                    continue;
                }

                model.CoverUrl = game.ThumbnailUrl;
                await library.UpsertAsync(model);
                row.ApplyModel(model, _apiBaseUrl);
            }
            catch
            {
                // best-effort
            }
        }
    }

    private void RebuildShelves()
    {
        RecentGames.Clear();
        foreach (var game in Games
                     .Where(g => g.LastPlayedUtc is not null)
                     .OrderByDescending(g => g.LastPlayedUtc)
                     .Take(8))
        {
            RecentGames.Add(game);
        }

        AllGames.Clear();
        foreach (var game in Games.OrderBy(g => g.Title))
        {
            AllGames.Add(game);
        }

        OnPropertyChanged(nameof(AllGamesHeader));
        OnPropertyChanged(nameof(ShowRecent));
    }

    private void ApplyFilter()
    {
        FilteredGames.Clear();
        IEnumerable<LibraryGameItem> query = Games;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(g =>
                g.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || g.Symbol.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var game in query.OrderBy(g => g.Title))
        {
            FilteredGames.Add(game);
        }
    }
}

public partial class LibraryGameItem : ObservableObject
{
    public required int GameId { get; init; }
    public required int BuildId { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }

    public DateTimeOffset? LastPlayedUtc { get; private set; }
    public string? CoverUrl { get; private set; }
    public string? TargetVersion { get; private set; }

    [ObservableProperty]
    public partial string TileLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlatformText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string VersionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string IdText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ExecutablePath { get; set; }

    [ObservableProperty]
    public partial string? CoverImageUrl { get; set; }

    [ObservableProperty]
    public partial bool HasCover { get; set; }

    [ObservableProperty]
    public partial bool CanLaunch { get; set; }

    [ObservableProperty]
    public partial bool CanInstall { get; set; }

    [ObservableProperty]
    public partial bool CanUninstall { get; set; }

    [ObservableProperty]
    public partial bool IsInstalling { get; set; }

    public double InstallProgressMaxHeight => IsInstalling ? 48 : 0;

    partial void OnIsInstallingChanged(bool value) =>
        OnPropertyChanged(nameof(InstallProgressMaxHeight));

    [ObservableProperty]
    public partial double InstallProgressFraction { get; set; }

    [ObservableProperty]
    public partial string InstallProgressText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastPlayedText { get; set; } = "Niegrana";

    [ObservableProperty]
    public partial string PlaytimeText { get; set; } = string.Empty;

    public bool HasPlaytime => !string.IsNullOrWhiteSpace(PlaytimeText);

    partial void OnPlaytimeTextChanged(string value) => OnPropertyChanged(nameof(HasPlaytime));

    public static LibraryGameItem FromModel(GameLibraryItem item, string apiBaseUrl)
    {
        var vm = new LibraryGameItem
        {
            GameId = item.GameId,
            BuildId = item.BuildId,
            Title = item.Title,
            Symbol = item.Symbol
        };
        vm.ApplyModel(item, apiBaseUrl);
        return vm;
    }

    public void ApplyModel(GameLibraryItem item, string apiBaseUrl)
    {
        TileLabel = string.IsNullOrWhiteSpace(item.Symbol)
            ? item.Title[..Math.Min(2, item.Title.Length)].ToUpperInvariant()
            : item.Symbol[..Math.Min(3, item.Symbol.Length)].ToUpperInvariant();
        PlatformText = item.Platform + (string.IsNullOrWhiteSpace(item.Architecture) ? "" : $" ({item.Architecture})");
        VersionText = string.IsNullOrWhiteSpace(item.InstalledVersion)
            ? (string.IsNullOrWhiteSpace(item.TargetVersion) ? "—" : $"Cel: {item.TargetVersion}")
            : item.InstalledVersion!;
        IdText = $"ID: {item.GameId}";
        ExecutablePath = item.ExecutablePath;
        CoverUrl = item.CoverUrl;
        TargetVersion = item.TargetVersion;
        LastPlayedUtc = item.LastPlayedUtc;
        CoverImageUrl = ResolveCoverImage(item, apiBaseUrl);
        HasCover = !string.IsNullOrWhiteSpace(CoverImageUrl);
        LastPlayedText = item.LastPlayedUtc is null
            ? "Niegrana"
            : item.LastPlayedUtc.Value.LocalDateTime.ToString("g");
        PlaytimeText = item.TotalPlayTimeSeconds > 0
            ? PlaytimeFormat.ToPolishTotal(item.TotalPlayTimeSeconds)
            : string.Empty;
        ApplyInstallState(item.InstallState);
    }

    private static string? ResolveCoverImage(GameLibraryItem item, string apiBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(item.CustomCoverPath) && File.Exists(item.CustomCoverPath))
        {
            return new Uri(item.CustomCoverPath).AbsoluteUri;
        }

        return string.IsNullOrWhiteSpace(item.CoverUrl)
            ? null
            : InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, item.CoverUrl);
    }

    public void ApplyInstallState(InstallState state)
    {
        StateText = state switch
        {
            InstallState.Installed => "Zainstalowana",
            InstallState.NotInstalled => "W bibliotece (niezainstalowana)",
            InstallState.Downloading => "Pobieranie…",
            InstallState.Installing => "Instalacja…",
            InstallState.Failed => "Błąd instalacji",
            InstallState.Updating => "Aktualizacja…",
            InstallState.Queued => "W kolejce",
            _ => state.ToString()
        };

        CanLaunch = state == InstallState.Installed
            && !string.IsNullOrWhiteSpace(ExecutablePath);
        CanInstall = state is InstallState.NotInstalled or InstallState.Failed;
        CanUninstall = state == InstallState.Installed;
    }
}
