using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;

namespace SGSClient.UI.ViewModels;

public partial class WishlistViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl;
    private Action<int>? _openDetails;
    private Action? _openAccount;

    public WishlistViewModel()
        : this(null)
    {
    }

    public WishlistViewModel(
        ISgsApiService? api,
        IServiceScopeFactory? scopes = null,
        IConfiguration? configuration = null,
        IAuthSession? auth = null)
    {
        _api = api;
        _scopes = scopes;
        _auth = auth;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = "Ładowanie listy życzeń…";
    }

    public void SetOpenDetailsHandler(Action<int> openDetails) => _openDetails = openDetails;
    public void SetOpenAccountHandler(Action openAccount) => _openAccount = openAccount;

    public string Title { get; } = "Lista życzeń";

    public ObservableCollection<GameListItem> Games { get; } = [];

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasGames { get; set; }

    [ObservableProperty]
    public partial bool ShowEmpty { get; set; }

    [ObservableProperty]
    public partial bool NeedsLogin { get; set; }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_api is null)
        {
            StatusText = "Brak klienta API.";
            ShowEmpty = true;
            HasGames = false;
            NeedsLogin = false;
            return;
        }

        if (_auth is { IsAvailable: true, IsLoggedIn: false })
        {
            Games.Clear();
            HasGames = false;
            ShowEmpty = true;
            NeedsLogin = true;
            StatusText = "Zaloguj się, żeby zobaczyć listę życzeń.";
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        NeedsLogin = false;
        try
        {
            StatusText = "Ładowanie listy życzeń…";
            var owned = await GetOwnedGameIdsAsync();
            var wish = await _api.GetWishlistAsync();
            Games.Clear();
            foreach (var game in wish)
            {
                Games.Add(GameListItem.FromDto(game, owned.Contains(game.Id), _apiBaseUrl, inWishlist: true));
            }

            HasGames = Games.Count > 0;
            ShowEmpty = !HasGames;
            StatusText = HasGames
                ? $"{Games.Count} gier na liście życzeń"
                : "Lista życzeń jest pusta. Dodawaj gry ze Sklepu.";
        }
        catch (Exception ex)
        {
            HasGames = false;
            ShowEmpty = true;
            StatusText = $"Lista życzeń: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenDetails(GameListItem? item)
    {
        if (item is not null)
        {
            _openDetails?.Invoke(item.Id);
        }
    }

    [RelayCommand]
    private void OpenAccount() => _openAccount?.Invoke();

    [RelayCommand]
    private async Task AddToLibraryAsync(GameListItem? item)
    {
        if (item is null || !item.CanAdd || _scopes is null || _api is null)
        {
            return;
        }

        if (_auth is { IsAvailable: true, IsLoggedIn: false })
        {
            StatusText = "Zaloguj się, żeby dodać grę do biblioteki.";
            _openAccount?.Invoke();
            return;
        }

        try
        {
            var claimed = await _api.ClaimGameAsync(item.Id);
            if (claimed is null)
            {
                StatusText = "Nie udało się dodać gry do biblioteki.";
                return;
            }

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
                TargetVersion = item.Version,
                CoverUrl = item.CoverUrl,
                LogoUrl = item.LogoUrl
            });

            item.InLibrary = true;
            StatusText = $"Dodano do biblioteki: {item.Title}";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Biblioteka: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(GameListItem? item)
    {
        if (item is null || _api is null)
        {
            return;
        }

        try
        {
            if (await _api.RemoveFromWishlistAsync(item.Id))
            {
                StatusText = $"Usunięto z listy życzeń: {item.Title}";
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Lista życzeń: {ex.Message}";
        }
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
}
