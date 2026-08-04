using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;

namespace SGSClient.UI.ViewModels;

public partial class LibraryViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly IGameLauncherService? _launcher;

    public LibraryViewModel()
    {
        StatusText = "Biblioteka niedostępna";
    }

    public LibraryViewModel(IServiceScopeFactory? scopes, IGameLauncherService? launcher = null)
    {
        _scopes = scopes;
        _launcher = launcher;
        StatusText = "Ładowanie biblioteki…";
    }

    public string Title { get; } = "Biblioteka";

    public ObservableCollection<LibraryGameItem> Games { get; } = [];

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _hasGames;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private bool _isBusy;

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
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var items = await library.GetAllAsync();

            Games.Clear();
            foreach (var item in items.OrderBy(x => x.Title))
            {
                Games.Add(LibraryGameItem.FromModel(item));
            }

            HasGames = Games.Count > 0;
            ShowEmpty = !HasGames;
            StatusText = HasGames
                ? $"{Games.Count} gier w bibliotece"
                : "Biblioteka jest pusta. Dodaj gry ze Sklepu.";
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
    private async Task RemoveAsync(LibraryGameItem? item)
    {
        if (item is null || _scopes is null)
        {
            return;
        }

        using var scope = _scopes.CreateScope();
        var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
        await library.RemoveAsync(item.GameId);
        Games.Remove(item);
        HasGames = Games.Count > 0;
        ShowEmpty = !HasGames;
        StatusText = HasGames
            ? $"{Games.Count} gier w bibliotece"
            : "Biblioteka jest pusta. Dodaj gry ze Sklepu.";
    }

    [RelayCommand]
    private async Task LaunchAsync(LibraryGameItem? item)
    {
        if (item is null || _launcher is null || string.IsNullOrWhiteSpace(item.ExecutablePath))
        {
            StatusText = item is null
                ? "Brak gry."
                : "Gra nie jest jeszcze zainstalowana (brak pliku exe). Pobieranie wkrótce.";
            return;
        }

        try
        {
            await _launcher.LaunchAsync(item.ExecutablePath);
            if (_scopes is not null)
            {
                using var scope = _scopes.CreateScope();
                var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
                var existing = await library.GetByGameIdAsync(item.GameId);
                if (existing is not null)
                {
                    existing.LastPlayedUtc = DateTimeOffset.UtcNow;
                    await library.UpsertAsync(existing);
                    item.LastPlayedText = "Właśnie grane";
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się uruchomić: {ex.Message}";
        }
    }
}

public partial class LibraryGameItem : ObservableObject
{
    public required int GameId { get; init; }
    public required int BuildId { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string TileLabel { get; init; } = string.Empty;
    public string PlatformText { get; init; } = string.Empty;
    public string VersionText { get; init; } = string.Empty;
    public string StateText { get; init; } = string.Empty;
    public string? ExecutablePath { get; init; }
    public bool CanLaunch => !string.IsNullOrWhiteSpace(ExecutablePath);

    [ObservableProperty]
    private string _lastPlayedText = "Niegrana";

    public static LibraryGameItem FromModel(GameLibraryItem item) => new()
    {
        GameId = item.GameId,
        BuildId = item.BuildId,
        Title = item.Title,
        Symbol = item.Symbol,
        TileLabel = string.IsNullOrWhiteSpace(item.Symbol)
            ? item.Title[..Math.Min(2, item.Title.Length)].ToUpperInvariant()
            : item.Symbol[..Math.Min(3, item.Symbol.Length)].ToUpperInvariant(),
        PlatformText = item.Platform.ToString() + (string.IsNullOrWhiteSpace(item.Architecture) ? "" : $" ({item.Architecture})"),
        VersionText = string.IsNullOrWhiteSpace(item.InstalledVersion) ? "—" : item.InstalledVersion!,
        StateText = item.InstallState switch
        {
            InstallState.Installed => "Zainstalowana",
            InstallState.NotInstalled => "W bibliotece (niezainstalowana)",
            InstallState.Downloading => "Pobieranie…",
            InstallState.Installing => "Instalacja…",
            InstallState.Failed => "Błąd instalacji",
            InstallState.Updating => "Aktualizacja…",
            InstallState.Queued => "W kolejce",
            _ => item.InstallState.ToString()
        },
        ExecutablePath = item.ExecutablePath,
        LastPlayedText = item.LastPlayedUtc is null
            ? "Niegrana"
            : item.LastPlayedUtc.Value.LocalDateTime.ToString("g")
    };
}
