using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.ViewModels;

public partial class AdminViewModel : ViewModelBase
{
    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private Func<AdminGameItem, Task<bool>>? _editGame;
    private Func<AdminGameItem, Task<bool>>? _confirmDelete;

    public AdminViewModel()
    {
        StatusText = "Admin niedostępny";
    }

    public AdminViewModel(ISgsApiService? api, IAuthSession? auth)
    {
        _api = api;
        _auth = auth;
        StatusText = "Ładowanie panelu admina…";
    }

    public string Title { get; } = "Admin";

    public ObservableCollection<AdminGameItem> Games { get; } = [];
    public ObservableCollection<AdminUserItem> Users { get; } = [];

    [ObservableProperty]
    private string _selectedTab = "games";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showGames;

    [ObservableProperty]
    private bool _showUsers;

    [ObservableProperty]
    private bool _showEmpty;

    public bool IsGamesTab => SelectedTab == "games";
    public bool IsUsersTab => SelectedTab == "users";

    public void SetEditHandler(Func<AdminGameItem, Task<bool>> edit) => _editGame = edit;
    public void SetConfirmDeleteHandler(Func<AdminGameItem, Task<bool>> confirm) => _confirmDelete = confirm;

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsGamesTab));
        OnPropertyChanged(nameof(IsUsersTab));
        ShowGames = value == "games" && Games.Count > 0;
        ShowUsers = value == "users" && Users.Count > 0;
        ShowEmpty = value == "games" ? Games.Count == 0 : Users.Count == 0;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_api is null)
        {
            StatusText = "API niedostępne.";
            ShowEmpty = true;
            return;
        }

        if (_auth?.IsAvailable == true && !_auth.IsLoggedIn)
        {
            Games.Clear();
            Users.Clear();
            ShowEmpty = true;
            ShowGames = false;
            ShowUsers = false;
            StatusText = "Zaloguj się jako admin (Profil), aby zarządzać katalogiem.";
            return;
        }

        IsBusy = true;
        try
        {
            var games = await _api.GetAdminGamesAsync();
            var users = await _api.GetAdminUsersAsync();

            Games.Clear();
            foreach (var game in games.OrderBy(g => g.Kind).ThenBy(g => g.Title))
            {
                Games.Add(AdminGameItem.FromDto(game));
            }

            Users.Clear();
            foreach (var user in users.OrderBy(u => u.DisplayName))
            {
                Users.Add(AdminUserItem.FromDto(user));
            }

            ShowGames = IsGamesTab && Games.Count > 0;
            ShowUsers = IsUsersTab && Users.Count > 0;
            ShowEmpty = IsGamesTab ? Games.Count == 0 : Users.Count == 0;
            StatusText = Games.Count == 0 && Users.Count == 0
                ? "Brak danych lub brak uprawnień admina."
                : $"{Games.Count} pozycji katalogu · {Users.Count} użytkowników";
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd: {ex.Message}";
            ShowEmpty = true;
            ShowGames = false;
            ShowUsers = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            SelectedTab = tab;
        }
    }

    [RelayCommand]
    private async Task EditAsync(AdminGameItem? item)
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

        var updated = await _api.UpdateAdminGameAsync(item.Id, new UpdateGameRequestDto
        {
            Title = item.EditTitle?.Trim(),
            Description = item.EditDescription,
            DraftP = item.EditDraft,
            FeaturedP = item.EditFeatured
        });

        StatusText = updated is null ? "Nie udało się zapisać." : $"Zapisano: {updated.Title}";
        if (updated is not null)
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleDraftAsync(AdminGameItem? item)
    {
        if (item is null || _api is null)
        {
            return;
        }

        var updated = await _api.UpdateAdminGameAsync(item.Id, new UpdateGameRequestDto { DraftP = !item.DraftP });
        StatusText = updated is null
            ? "Nie udało się zmienić statusu szkicu."
            : updated.DraftP
                ? $"Ukryto w sklepie: {updated.Title}"
                : $"Przywrócono do sklepu: {updated.Title}";
        if (updated is not null)
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleFeaturedAsync(AdminGameItem? item)
    {
        if (item is null || _api is null)
        {
            return;
        }

        var updated = await _api.UpdateAdminGameAsync(item.Id, new UpdateGameRequestDto { FeaturedP = !item.FeaturedP });
        StatusText = updated is null
            ? "Nie udało się zmienić wyróżnienia."
            : updated.FeaturedP
                ? $"Wyróżniono: {updated.Title}"
                : $"Usunięto wyróżnienie: {updated.Title}";
        if (updated is not null)
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(AdminGameItem? item)
    {
        if (item is null || _api is null)
        {
            return;
        }

        if (_confirmDelete is not null && !await _confirmDelete(item))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var deleted = await _api.DeleteAdminGameAsync(item.Id);
            StatusText = deleted
                ? $"Usunięto: {item.Title}"
                : "Nie udało się usunąć (sprawdź powiązane DLC).";
            if (deleted)
            {
                await LoadAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed class AdminGameItem
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? Description { get; init; }
    public bool DraftP { get; init; }
    public bool FeaturedP { get; init; }
    public bool IsDlc { get; init; }
    public string? OwnerDisplayName { get; init; }
    public string? OwnerEmail { get; init; }

    public string KindLabel => IsDlc ? "DLC" : "Gra";
    public string MetaText =>
        $"{Symbol} · {KindLabel} · {(DraftP ? "Szkic" : "Opublikowana")}"
        + (FeaturedP ? " · Wyróżnione" : "")
        + (string.IsNullOrWhiteSpace(OwnerDisplayName) ? "" : $" · {OwnerDisplayName}");

    public string? EditTitle { get; set; }
    public string? EditDescription { get; set; }
    public bool EditDraft { get; set; }
    public bool EditFeatured { get; set; }

    public static AdminGameItem FromDto(GameDto game) => new()
    {
        Id = game.Id,
        Title = game.Title,
        Symbol = game.Symbol,
        Description = game.Description,
        DraftP = game.DraftP,
        FeaturedP = game.FeaturedP,
        IsDlc = game.Kind == GameContentKind.Dlc,
        OwnerDisplayName = game.OwnerDisplayName,
        OwnerEmail = game.OwnerEmail,
        EditTitle = game.Title,
        EditDescription = game.Description,
        EditDraft = game.DraftP,
        EditFeatured = game.FeaturedP
    };
}

public sealed class AdminUserItem
{
    public required int Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string CreatedText { get; init; } = "—";
    public string LastLoginText { get; init; } = "—";
    public string MetaText =>
        $"#{Id}"
        + (string.IsNullOrWhiteSpace(Email) ? "" : $" · {Email}")
        + $" · dołączono {CreatedText} · ostatnio {LastLoginText}";

    public static AdminUserItem FromDto(AdminUserDto user) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        Email = user.Email,
        CreatedText = user.CreatedAtUtc.LocalDateTime.ToString("d MMM yyyy"),
        LastLoginText = user.LastLoginUtc?.LocalDateTime.ToString("d MMM yyyy") ?? "—"
    };
}
