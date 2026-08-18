using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;

namespace SGSClient.UI.ViewModels;

public partial class AdminViewModel : ViewModelBase
{
    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl = new ApiClientSettings().BaseUrl;
    private Func<AdminGameItem, Task<bool>>? _editGame;
    private Func<AdminGameItem, Task<bool>>? _confirmDelete;

    public AdminViewModel()
    {
        StatusText = "Admin niedostępny";
    }

    public AdminViewModel(ISgsApiService? api, IAuthSession? auth, IConfiguration? configuration = null)
    {
        _api = api;
        _auth = auth;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = "Ładowanie panelu admina…";
    }

    public string Title { get; } = "Admin";

    public ObservableCollection<AdminGameItem> Games { get; } = [];
    public ObservableCollection<AdminGameItem> FilteredGames { get; } = [];
    public ObservableCollection<AdminGameItem> FeaturedGames { get; } = [];
    public ObservableCollection<AdminUserItem> Users { get; } = [];

    [ObservableProperty]
    private string _selectedTab = "games";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showEmpty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _filter = "all";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private AdminGameItem? _selectedGame;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHeroPreview))]
    private AdminGameItem? _heroPreview;

    [ObservableProperty]
    private bool _hasFeatured;

    [ObservableProperty]
    private string _listSummary = string.Empty;

    [ObservableProperty]
    private string _featuredSummary = string.Empty;

    public bool HasSelection => SelectedGame is not null;
    public bool HasHeroPreview => HeroPreview is not null;
    public bool IsGamesTab => SelectedTab == "games";
    public bool IsFeaturedTab => SelectedTab == "featured";
    public bool IsUsersTab => SelectedTab == "users";

    public bool ShowGames => IsGamesTab && !ShowEmpty;
    public bool ShowFeatured => IsFeaturedTab && !ShowEmpty;
    public bool ShowUsers => IsUsersTab && !ShowEmpty;

    public bool SelectedPublished
    {
        get => SelectedGame?.IsPublished == true;
        set => _ = SetDraftAsync(!value);
    }

    public bool SelectedFeatured
    {
        get => SelectedGame?.FeaturedP == true;
        set => _ = SetFeaturedAsync(value);
    }

    public void SetEditHandler(Func<AdminGameItem, Task<bool>> edit) => _editGame = edit;
    public void SetConfirmDeleteHandler(Func<AdminGameItem, Task<bool>> confirm) => _confirmDelete = confirm;

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsGamesTab));
        OnPropertyChanged(nameof(IsFeaturedTab));
        OnPropertyChanged(nameof(IsUsersTab));
        ShowEmpty = value switch
        {
            "games" => FilteredGames.Count == 0,
            "featured" => FeaturedGames.Count == 0,
            _ => Users.Count == 0
        };
        NotifyPanels();
    }

    partial void OnShowEmptyChanged(bool value) => NotifyPanels();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedGameChanged(AdminGameItem? value)
    {
        OnPropertyChanged(nameof(SelectedPublished));
        OnPropertyChanged(nameof(SelectedFeatured));
    }

    private void NotifyPanels()
    {
        OnPropertyChanged(nameof(ShowGames));
        OnPropertyChanged(nameof(ShowFeatured));
        OnPropertyChanged(nameof(ShowUsers));
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
            FilteredGames.Clear();
            FeaturedGames.Clear();
            Users.Clear();
            SelectedGame = null;
            HeroPreview = null;
            ShowEmpty = true;
            StatusText = "Zaloguj się jako admin (Profil), aby zarządzać katalogiem.";
            return;
        }

        IsBusy = true;
        var selectedId = SelectedGame?.Id;
        try
        {
            var games = await _api.GetAdminGamesAsync();
            var users = await _api.GetAdminUsersAsync();

            Games.Clear();
            foreach (var game in games.OrderBy(g => g.Kind).ThenBy(g => g.Title))
            {
                Games.Add(AdminGameItem.FromDto(game, _apiBaseUrl));
            }

            Users.Clear();
            foreach (var user in users.OrderByDescending(u => u.Role).ThenBy(u => u.DisplayName))
            {
                var item = AdminUserItem.FromDto(user);
                ApplyRolePermissions(item);
                item.RoleChangeRequested += OnRoleChangeRequested;
                Users.Add(item);
            }

            ApplyFilter();
            RebuildFeatured();

            SelectedGame = selectedId is null
                ? FilteredGames.FirstOrDefault()
                : Games.FirstOrDefault(g => g.Id == selectedId) ?? FilteredGames.FirstOrDefault();

            StatusText = Games.Count == 0 && Users.Count == 0
                ? "Brak danych lub brak uprawnień admina."
                : $"{Games.Count} pozycji katalogu · {Users.Count} użytkowników";

            foreach (var game in Games)
            {
                _ = game.LoadCoverAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd: {ex.Message}";
            ShowEmpty = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private UserRole ViewerRole => _auth?.CurrentUser?.Role ?? UserRole.SuperAdmin;

    private int? ViewerId => _auth?.CurrentUser?.Id;

    private void ApplyRolePermissions(AdminUserItem item)
    {
        var viewerRole = ViewerRole;
        item.ViewerIsSuperAdmin = viewerRole >= UserRole.SuperAdmin;
        item.IsSelf = ViewerId is { } id && id == item.Id;
        item.CanEditRole = !item.IsRoleLocked
            && !item.IsSelf
            && (viewerRole >= UserRole.SuperAdmin || item.Role < UserRole.Admin);
    }

    private async void OnRoleChangeRequested(AdminUserItem item, UserRole role) =>
        await SetUserRoleAsync(item, role);

    private async Task SetUserRoleAsync(AdminUserItem item, UserRole role)
    {
        if (_api is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _api.UpdateAdminUserRoleAsync(item.Id, role);
            if (!result.Success || result.User is null)
            {
                StatusText = result.Error ?? "Nie udało się zmienić roli.";
                item.OnRoleReverted();
                return;
            }

            item.ApplyDto(result.User);
            ApplyRolePermissions(item);
            StatusText = $"{item.DisplayName}: rola {AdminRoles.Label(item.Role).ToLowerInvariant()}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var matches = Games.Where(g => Filter switch
        {
            "published" => !g.DraftP,
            "drafts" => g.DraftP,
            "featured" => g.FeaturedP,
            "dlc" => g.IsDlc,
            _ => true
        });

        if (query.Length > 0)
        {
            matches = matches.Where(g =>
                g.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || g.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredGames.Clear();
        foreach (var game in matches)
        {
            FilteredGames.Add(game);
        }

        ListSummary = $"{FilteredGames.Count} z {Games.Count}";
        ShowEmpty = IsGamesTab
            ? FilteredGames.Count == 0
            : IsFeaturedTab ? FeaturedGames.Count == 0 : Users.Count == 0;
        NotifyPanels();
    }

    private void RebuildFeatured()
    {
        FeaturedGames.Clear();
        foreach (var game in Games
                     .Where(g => g.FeaturedP)
                     .OrderBy(g => g.FeaturedOrder ?? int.MaxValue)
                     .ThenBy(g => g.Title))
        {
            FeaturedGames.Add(game);
        }

        for (var i = 0; i < FeaturedGames.Count; i++)
        {
            FeaturedGames[i].PositionLabel = i == 0 ? "Hero" : $"#{i + 1}";
        }

        HasFeatured = FeaturedGames.Count > 0;
        HeroPreview = FeaturedGames.FirstOrDefault();
        FeaturedSummary = HasFeatured
            ? $"{FeaturedGames.Count} wyróżnionych · pierwsza pozycja trafia na hero"
            : "Brak wyróżnionych gier. Włącz przełącznik przy grze na liście.";
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
    private void SetFilter(string? filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            Filter = filter;
        }
    }

    [RelayCommand]
    private async Task EditAsync(AdminGameItem? item)
    {
        item ??= SelectedGame;
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

    private async Task SetDraftAsync(bool draft)
    {
        var item = SelectedGame;
        if (item is null || _api is null || item.DraftP == draft)
        {
            return;
        }

        var updated = await _api.UpdateAdminGameAsync(item.Id, new UpdateGameRequestDto { DraftP = draft });
        if (updated is null)
        {
            StatusText = "Nie udało się zmienić statusu publikacji.";
            OnPropertyChanged(nameof(SelectedPublished));
            return;
        }

        item.ApplyDto(updated);
        ApplyFilter();
        OnPropertyChanged(nameof(SelectedPublished));
        StatusText = updated.DraftP
            ? $"Ukryto w sklepie: {updated.Title}"
            : $"Opublikowano: {updated.Title}";
    }

    private async Task SetFeaturedAsync(bool featured)
    {
        var item = SelectedGame;
        if (item is null || _api is null || item.FeaturedP == featured)
        {
            return;
        }

        var updated = await _api.UpdateAdminGameAsync(item.Id, new UpdateGameRequestDto
        {
            FeaturedP = featured,
            FeaturedOrder = featured ? FeaturedGames.Count : -1
        });

        if (updated is null)
        {
            StatusText = "Nie udało się zmienić wyróżnienia.";
            OnPropertyChanged(nameof(SelectedFeatured));
            return;
        }

        item.ApplyDto(updated);
        ApplyFilter();
        RebuildFeatured();
        OnPropertyChanged(nameof(SelectedFeatured));
        StatusText = updated.FeaturedP
            ? $"Wyróżniono: {updated.Title}"
            : $"Usunięto wyróżnienie: {updated.Title}";
    }

    [RelayCommand]
    private Task MoveFeaturedUpAsync(AdminGameItem? item) => MoveFeaturedAsync(item, -1);

    [RelayCommand]
    private Task MoveFeaturedDownAsync(AdminGameItem? item) => MoveFeaturedAsync(item, 1);

    private async Task MoveFeaturedAsync(AdminGameItem? item, int delta)
    {
        if (item is null || _api is null)
        {
            return;
        }

        var index = FeaturedGames.IndexOf(item);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= FeaturedGames.Count)
        {
            return;
        }

        FeaturedGames.Move(index, target);
        await PersistFeaturedOrderAsync();
    }

    private async Task PersistFeaturedOrderAsync()
    {
        if (_api is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var ordered = FeaturedGames.ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var updated = await _api.UpdateAdminGameAsync(ordered[i].Id, new UpdateGameRequestDto
                {
                    FeaturedOrder = i
                });
                if (updated is not null)
                {
                    ordered[i].ApplyDto(updated);
                }

                ordered[i].PositionLabel = i == 0 ? "Hero" : $"#{i + 1}";
            }

            HeroPreview = FeaturedGames.FirstOrDefault();
            StatusText = "Zapisano kolejność wyróżnionych.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się zapisać kolejności: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(AdminGameItem? item)
    {
        item ??= SelectedGame;
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
                SelectedGame = null;
                await LoadAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class AdminGameItem : ObservableObject
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public string? Description { get; private set; }
    public string? HardwareRequirements { get; private set; }
    public string? OtherInformation { get; private set; }
    public string? GenreSymbol { get; private set; }
    public string? EngineSymbol { get; private set; }
    public string? CoverUrl { get; private set; }
    public string? IntroUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public List<GameScreenshotDto> Screenshots { get; private set; } = [];
    public bool IsDlc { get; init; }
    public string? OwnerDisplayName { get; init; }
    public string? OwnerEmail { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    [NotifyPropertyChangedFor(nameof(MetaText))]
    [NotifyPropertyChangedFor(nameof(IsPublished))]
    private bool _draftP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetaText))]
    private bool _featuredP;

    [ObservableProperty]
    private int? _featuredOrder;

    [ObservableProperty]
    private string _positionLabel = string.Empty;

    [ObservableProperty]
    private Bitmap? _coverBitmap;

    [ObservableProperty]
    private bool _hasCover;

    public bool IsPublished => !DraftP;
    public string TileLabel => string.IsNullOrWhiteSpace(Symbol)
        ? Title[..Math.Min(2, Title.Length)].ToUpperInvariant()
        : Symbol[..Math.Min(3, Symbol.Length)].ToUpperInvariant();

    public string KindLabel => IsDlc ? "DLC" : "Gra";
    public string StateLabel => DraftP ? "Szkic" : "Opublikowana";
    public string OwnerText => string.IsNullOrWhiteSpace(OwnerDisplayName) ? "—" : OwnerDisplayName!;
    public string MetaText => $"{Symbol} · {KindLabel} · {StateLabel}";
    public string DescriptionText => string.IsNullOrWhiteSpace(Description) ? "Brak opisu." : Description!;
    public string GenreText => string.IsNullOrWhiteSpace(GenreSymbol) ? "—" : GenreSymbol!;
    public string EngineText => string.IsNullOrWhiteSpace(EngineSymbol) ? "—" : EngineSymbol!;
    public string CoverText => string.IsNullOrWhiteSpace(CoverUrl) ? "Brak okładki" : "Okładka ustawiona";
    public string IntroText => string.IsNullOrWhiteSpace(IntroUrl) ? "Brak trailera" : "Trailer ustawiony";
    public string ScreenshotsText => $"{Screenshots.Count} screenów";

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

    public static AdminGameItem FromDto(GameDto game, string apiBaseUrl)
    {
        var item = new AdminGameItem
        {
            Id = game.Id,
            Title = game.Title,
            Symbol = game.Symbol,
            IsDlc = game.Kind == GameContentKind.Dlc,
            OwnerDisplayName = game.OwnerDisplayName,
            OwnerEmail = game.OwnerEmail
        };
        item.ApplyDto(game, apiBaseUrl);
        return item;
    }

    public void ApplyDto(GameDto game, string? apiBaseUrl = null)
    {
        Description = game.Description;
        HardwareRequirements = game.HardwareRequirements;
        OtherInformation = game.OtherInformation;
        GenreSymbol = game.GenreSymbol;
        EngineSymbol = game.EngineSymbol;
        CoverUrl = game.CoverUrl;
        IntroUrl = game.IntroUrl;
        Screenshots = game.Screenshots?.ToList()
            ?? game.ScreenshotUrls.Select((url, i) => new GameScreenshotDto { Id = -(i + 1), Url = url }).ToList();
        DraftP = game.DraftP;
        FeaturedP = game.FeaturedP;
        FeaturedOrder = game.FeaturedOrder;

        if (apiBaseUrl is not null)
        {
            CoverImageUrl = string.IsNullOrWhiteSpace(game.CoverUrl)
                ? null
                : InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, game.CoverUrl);
        }

        EditTitle = game.Title;
        EditDescription = game.Description;
        EditHardwareRequirements = game.HardwareRequirements;
        EditOtherInformation = game.OtherInformation;
        EditGenre = game.GenreSymbol ?? string.Empty;
        EditEngine = game.EngineSymbol ?? string.Empty;
        EditDraft = game.DraftP;
        EditFeatured = game.FeaturedP;

        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(GenreText));
        OnPropertyChanged(nameof(EngineText));
        OnPropertyChanged(nameof(CoverText));
        OnPropertyChanged(nameof(IntroText));
        OnPropertyChanged(nameof(ScreenshotsText));
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
}

public partial class AdminUserItem : ObservableObject
{
    public required int Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? PlayerTag { get; init; }
    public bool IsRoleLocked { get; init; }
    public string CreatedText { get; init; } = "—";
    public string LastLoginText { get; init; } = "—";

    /// <summary>Raised when the role picker changes; the view model persists it.</summary>
    public event Action<AdminUserItem, UserRole>? RoleChangeRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleLabel))]
    [NotifyPropertyChangedFor(nameof(RoleIndex))]
    private UserRole _role;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleOptions))]
    [NotifyPropertyChangedFor(nameof(RoleHint))]
    private bool _canEditRole;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleHint))]
    private bool _isSelf;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleOptions))]
    private bool _viewerIsSuperAdmin;

    public int RoleIndex
    {
        get => (int)Role;
        set
        {
            if (value >= 0 && (UserRole)value != Role)
            {
                RoleChangeRequested?.Invoke(this, (UserRole)value);
            }
        }
    }

    public IReadOnlyList<string> RoleOptions =>
        CanEditRole && !ViewerIsSuperAdmin ? AdminRoles.AssignableByAdmin : AdminRoles.All;

    public string RoleLabel => AdminRoles.Label(Role);

    public string RoleHint => IsRoleLocked
        ? "Konto właściciela — rola na stałe"
        : IsSelf
            ? "Nie możesz zmienić własnej roli"
            : CanEditRole
                ? string.Empty
                : "Tylko superadmin zmienia role adminów";

    public string Initials => string.IsNullOrWhiteSpace(DisplayName)
        ? "?"
        : DisplayName.Trim()[..1].ToUpperInvariant();

    public string MetaText =>
        (string.IsNullOrWhiteSpace(Email) ? $"#{Id}" : Email!)
        + (string.IsNullOrWhiteSpace(PlayerTag) ? "" : $" · @{PlayerTag}")
        + $" · dołączono {CreatedText} · ostatnio {LastLoginText}";

    public void ApplyDto(AdminUserDto user) => Role = user.Role;

    /// <summary>Pushes the unchanged role back into the picker after a refused change.</summary>
    public void OnRoleReverted() => OnPropertyChanged(nameof(RoleIndex));

    public static AdminUserItem FromDto(AdminUserDto user) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        Email = user.Email,
        PlayerTag = user.PlayerTag,
        IsRoleLocked = user.IsRoleLocked,
        Role = user.Role,
        CreatedText = user.CreatedAtUtc.LocalDateTime.ToString("d MMM yyyy"),
        LastLoginText = user.LastLoginUtc?.LocalDateTime.ToString("d MMM yyyy") ?? "—"
    };
}

public static class AdminRoles
{
    public static IReadOnlyList<string> All { get; } = ["Użytkownik", "Twórca", "Admin", "Superadmin"];

    /// <summary>An admin may only move accounts between the two lowest roles.</summary>
    public static IReadOnlyList<string> AssignableByAdmin { get; } = [All[0], All[1]];

    public static string Label(UserRole role) => All[Math.Clamp((int)role, 0, All.Count - 1)];
}
