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
            HardwareRequirements = item.EditHardwareRequirements,
            OtherInformation = item.EditOtherInformation,
            Genre = item.EditGenre,
            Engine = item.EditEngine,
            DraftP = item.EditDraft,
            FeaturedP = item.EditFeatured
        });

        if (updated is null)
        {
            StatusText = "Nie udało się zapisać metadanych.";
            return;
        }

        var mediaOk = await ApplyMediaEditsAsync(item.Id, item, asAdmin: true);
        StatusText = mediaOk
            ? $"Zapisano: {updated.Title}"
            : $"Zapisano metadane „{updated.Title}”, ale część mediów się nie udała.";
        await LoadAsync();
    }

    private async Task<bool> ApplyMediaEditsAsync(int gameId, AdminGameItem item, bool asAdmin)
    {
        if (_api is null)
        {
            return false;
        }

        var ok = true;

        foreach (var screenshotId in item.ScreenshotIdsToRemove.Where(id => id > 0).Distinct())
        {
            if (await _api.RemoveGameScreenshotAsync(gameId, screenshotId, asAdmin) is null)
            {
                ok = false;
            }
        }

        foreach (var path in item.PendingScreenshotPaths)
        {
            if (await _api.AddGameScreenshotAsync(gameId, path, asAdmin) is null)
            {
                ok = false;
            }
        }

        if (item.ClearCover)
        {
            if (await _api.ClearGameCoverAsync(gameId, asAdmin) is null)
            {
                ok = false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(item.PendingCoverPath))
        {
            if (await _api.UploadGameCoverAsync(gameId, item.PendingCoverPath, asAdmin) is null)
            {
                ok = false;
            }
        }

        if (item.ClearIntro)
        {
            if (await _api.ClearGameIntroAsync(gameId, asAdmin) is null)
            {
                ok = false;
            }
        }
        else if (!string.IsNullOrWhiteSpace(item.PendingIntroPath))
        {
            if (await _api.UploadGameIntroAsync(gameId, item.PendingIntroPath, asAdmin) is null)
            {
                ok = false;
            }
        }

        return ok;
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
    public string? HardwareRequirements { get; init; }
    public string? OtherInformation { get; init; }
    public string? GenreSymbol { get; init; }
    public string? EngineSymbol { get; init; }
    public string? CoverUrl { get; init; }
    public string? IntroUrl { get; init; }
    public List<GameScreenshotDto> Screenshots { get; init; } = [];
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
    public string? EditHardwareRequirements { get; set; }
    public string? EditOtherInformation { get; set; }
    public string? EditGenre { get; set; }
    public string? EditEngine { get; set; }
    public bool EditDraft { get; set; }
    public bool EditFeatured { get; set; }

    public string? PendingCoverPath { get; set; }
    public bool ClearCover { get; set; }
    public string? PendingIntroPath { get; set; }
    public bool ClearIntro { get; set; }
    public List<string> PendingScreenshotPaths { get; } = [];
    public List<int> ScreenshotIdsToRemove { get; } = [];

    public static AdminGameItem FromDto(GameDto game) => new()
    {
        Id = game.Id,
        Title = game.Title,
        Symbol = game.Symbol,
        Description = game.Description,
        HardwareRequirements = game.HardwareRequirements,
        OtherInformation = game.OtherInformation,
        GenreSymbol = game.GenreSymbol,
        EngineSymbol = game.EngineSymbol,
        CoverUrl = game.CoverUrl,
        IntroUrl = game.IntroUrl,
        Screenshots = game.Screenshots?.ToList()
            ?? game.ScreenshotUrls.Select((url, i) => new GameScreenshotDto { Id = -(i + 1), Url = url }).ToList(),
        DraftP = game.DraftP,
        FeaturedP = game.FeaturedP,
        IsDlc = game.Kind == GameContentKind.Dlc,
        OwnerDisplayName = game.OwnerDisplayName,
        OwnerEmail = game.OwnerEmail,
        EditTitle = game.Title,
        EditDescription = game.Description,
        EditHardwareRequirements = game.HardwareRequirements,
        EditOtherInformation = game.OtherInformation,
        EditGenre = game.GenreSymbol ?? string.Empty,
        EditEngine = game.EngineSymbol ?? string.Empty,
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
