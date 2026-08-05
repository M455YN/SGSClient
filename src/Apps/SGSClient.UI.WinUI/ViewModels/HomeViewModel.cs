using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private bool _loaded;

    public HomeViewModel(ISgsApiService? api)
    {
        _api = api;
    }

    public string Brand { get; } = "SGSClient";
    public string Headline { get; } = "Graj. Odkrywaj. Buduj bibliotekę.";
    public string Tagline { get; } = "Klient gier Szyr Game Studio — sklep, biblioteka i konto w jednym miejscu.";

    public ObservableCollection<HomeFeaturedItem> Featured { get; } = [];

    [ObservableProperty]
    public partial bool HasFeatured { get; set; }

    [ObservableProperty]
    public partial string FeaturedStatus { get; set; } = string.Empty;

    public event Action<string>? NavigateRequested;
    public event Action<int>? OpenGameRequested;

    [RelayCommand]
    private void OpenStore() => NavigateRequested?.Invoke("Games");

    [RelayCommand]
    private void OpenLibrary() => NavigateRequested?.Invoke("Library");

    [RelayCommand]
    private void OpenFeatured(HomeFeaturedItem? item)
    {
        if (item is not null)
        {
            OpenGameRequested?.Invoke(item.Id);
        }
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded || _api is null)
        {
            return;
        }

        try
        {
            FeaturedStatus = "Ładowanie wyróżnionych…";
            var games = await _api.GetGamesAsync();
            Featured.Clear();
            foreach (var game in games.Where(g => g.FeaturedP).Take(4))
            {
                Featured.Add(HomeFeaturedItem.From(game.Id, game.Title, game.Symbol, game.Description));
            }

            if (Featured.Count == 0)
            {
                foreach (var game in games.Take(3))
                {
                    Featured.Add(HomeFeaturedItem.From(game.Id, game.Title, game.Symbol, game.Description));
                }
            }

            _loaded = true;
            HasFeatured = Featured.Count > 0;
            FeaturedStatus = HasFeatured ? "Wyróżnione w sklepie" : "Brak gier w katalogu — uruchom Api.";
        }
        catch (Exception ex)
        {
            HasFeatured = false;
            FeaturedStatus = $"Nie udało się pobrać katalogu: {ex.Message}";
        }
    }
}

public sealed class HomeFeaturedItem
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string TileLabel { get; init; }
    public required string Blurb { get; init; }

    public static HomeFeaturedItem From(int id, string title, string symbol, string? description) => new()
    {
        Id = id,
        Title = title,
        TileLabel = string.IsNullOrWhiteSpace(symbol)
            ? title[..Math.Min(2, title.Length)].ToUpperInvariant()
            : symbol[..Math.Min(3, symbol.Length)].ToUpperInvariant(),
        Blurb = string.IsNullOrWhiteSpace(description)
            ? "Zobacz w sklepie"
            : description.Length > 90
                ? description[..87] + "…"
                : description
    };
}
