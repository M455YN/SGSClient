using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class StudioViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private Action? _addGame;
    private Action<string>? _addDlc;
    private Func<StudioContentItem, Task<bool>>? _editGame;
    private Func<StudioContentItem, Task<bool>>? _confirmDelete;

    public StudioViewModel(ISgsApiService? api, IAuthSession? auth)
    {
        _api = api;
        _auth = auth;
        StatusText = "Ładowanie Twoich gier…";
    }

    public string Title { get; } = "Moje gry";

    public ObservableCollection<StudioContentItem> Games { get; } = [];

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool HasGames { get; set; }
    [ObservableProperty] public partial bool ShowEmpty { get; set; }

    public void SetAddGameHandler(Action add) => _addGame = add;
    public void SetAddDlcHandler(Action<string> add) => _addDlc = add;
    public void SetEditHandler(Func<StudioContentItem, Task<bool>> edit) => _editGame = edit;
    public void SetConfirmDeleteHandler(Func<StudioContentItem, Task<bool>> confirm) => _confirmDelete = confirm;

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_api is null)
        {
            StatusText = "API niedostępne.";
            ShowEmpty = true;
            HasGames = false;
            return;
        }

        if (_auth?.IsAvailable == true && !_auth.IsLoggedIn)
        {
            Games.Clear();
            HasGames = false;
            ShowEmpty = true;
            StatusText = "Zaloguj się w Profilu, aby zobaczyć swoje gry.";
            return;
        }

        IsBusy = true;
        try
        {
            var all = await _api.GetMyGamesAsync();
            var baseGames = all.Where(g => g.Kind == GameContentKind.Game).OrderBy(g => g.Title).ToList();
            var dlcByParent = all
                .Where(g => g.Kind == GameContentKind.Dlc && g.ParentGameId is not null)
                .GroupBy(g => g.ParentGameId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            Games.Clear();
            foreach (var game in baseGames)
            {
                var item = StudioContentItem.FromGame(game);
                if (game.Dlcs.Count > 0)
                {
                    foreach (var dlc in game.Dlcs.OrderBy(d => d.Title))
                    {
                        item.Dlcs.Add(StudioContentItem.FromDlc(dlc, game.Symbol, game.DraftP));
                    }
                }
                else if (dlcByParent.TryGetValue(game.Id, out var ownedDlcs))
                {
                    foreach (var dlc in ownedDlcs.OrderBy(d => d.Title))
                    {
                        item.Dlcs.Add(StudioContentItem.FromGame(dlc));
                    }
                }

                Games.Add(item);
            }

            HasGames = Games.Count > 0;
            ShowEmpty = !HasGames;
            StatusText = HasGames
                ? $"{Games.Count} gier · {Games.Sum(g => g.Dlcs.Count)} DLC"
                : "Nie masz jeszcze opublikowanych gier. Dodaj pierwszą.";
        }
        catch (Exception ex)
        {
            HasGames = false;
            ShowEmpty = true;
            StatusText = $"Błąd ładowania: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddGame()
    {
        _addGame?.Invoke();
    }

    [RelayCommand]
    private void AddDlc(StudioContentItem? parent)
    {
        if (parent is null || string.IsNullOrWhiteSpace(parent.Symbol))
        {
            return;
        }

        _addDlc?.Invoke(parent.Symbol);
    }

    [RelayCommand]
    private async Task EditAsync(StudioContentItem? item)
    {
        if (item is null || _api is null || _editGame is null)
        {
            return;
        }

        var saved = await _editGame(item);
        if (!saved)
        {
            return;
        }

        var updated = await _api.UpdateGameAsync(item.Id, new UpdateGameRequestDto
        {
            Title = item.EditTitle?.Trim(),
            Description = item.EditDescription,
            DraftP = item.EditDraft
        });

        if (updated is null)
        {
            StatusText = "Nie udało się zapisać zmian.";
            return;
        }

        StatusText = $"Zapisano: {updated.Title}";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(StudioContentItem? item)
    {
        if (item is null || _api is null)
        {
            return;
        }

        if (_confirmDelete is not null)
        {
            var ok = await _confirmDelete(item);
            if (!ok)
            {
                return;
            }
        }

        IsBusy = true;
        try
        {
            var deleted = await _api.DeleteGameAsync(item.Id);
            StatusText = deleted
                ? $"Usunięto: {item.Title}"
                : "Nie udało się usunąć (sprawdź uprawnienia lub powiązane DLC).";
            if (deleted)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd usuwania: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class StudioContentItem : ObservableObject
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? Description { get; init; }
    public bool DraftP { get; init; }
    public bool IsDlc { get; init; }
    public string? ParentSymbol { get; init; }
    public ObservableCollection<StudioContentItem> Dlcs { get; } = [];

    public string KindLabel => IsDlc ? "DLC" : "Gra";
    public string DraftLabel => DraftP ? "Szkic" : "Opublikowana";
    public string MetaText => IsDlc
        ? $"{Symbol} · DLC · {DraftLabel}"
        : $"{Symbol} · {DraftLabel} · {Dlcs.Count} DLC";
    public bool HasDlcs => Dlcs.Count > 0;

    public string? EditTitle { get; set; }
    public string? EditDescription { get; set; }
    public bool EditDraft { get; set; }

    public static StudioContentItem FromGame(GameDto game) => new()
    {
        Id = game.Id,
        Title = game.Title,
        Symbol = game.Symbol,
        Description = game.Description,
        DraftP = game.DraftP,
        IsDlc = game.Kind == GameContentKind.Dlc,
        ParentSymbol = game.ParentSymbol,
        EditTitle = game.Title,
        EditDescription = game.Description,
        EditDraft = game.DraftP
    };

    public static StudioContentItem FromDlc(GameDlcDto dlc, string parentSymbol, bool parentDraft) => new()
    {
        Id = dlc.Id,
        Title = dlc.Title,
        Symbol = dlc.Symbol,
        Description = null,
        DraftP = parentDraft,
        IsDlc = true,
        ParentSymbol = parentSymbol,
        EditTitle = dlc.Title,
        EditDescription = null,
        EditDraft = parentDraft
    };
}
