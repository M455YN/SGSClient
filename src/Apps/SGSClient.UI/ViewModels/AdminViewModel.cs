using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;

namespace SGSClient.UI.ViewModels;

public partial class AdminViewModel : ViewModelBase
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl = new ApiClientSettings().BaseUrl;
    private Action? _addVersion;
    private Action<string>? _addDlc;
    private Func<AdminGameItem, Task<bool>>? _confirmDelete;
    private Func<Task<string?>>? _pickImage;
    private Func<Task<string?>>? _pickVideo;

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
    public IReadOnlyList<string> GenreOptions { get; } =
        SgsGameTaxonomy.Genres.Select(g => g.Symbol).Prepend("").ToArray();
    public IReadOnlyList<string> EngineOptions { get; } =
        SgsGameTaxonomy.Engines.Select(e => e.Symbol).Prepend("").ToArray();
    public IReadOnlyList<string> ReleaseStatusOptions { get; } = GameStoreLabels.ReleaseStatusOptions;
    public IReadOnlyList<string> AgeRatingOptions { get; } = GameStoreLabels.AgeRatingOptions;

    public ObservableCollection<AdminGameItem> Games { get; } = [];
    public ObservableCollection<AdminGameItem> FilteredGames { get; } = [];
    public ObservableCollection<AdminGameItem> FeaturedGames { get; } = [];
    public ObservableCollection<AdminUserItem> Users { get; } = [];
    public ObservableCollection<StudioVersionItem> Versions { get; } = [];
    public ObservableCollection<StudioStatBar> DailyBars { get; } = [];

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

    [ObservableProperty]
    private string _editorTab = "overview";

    [ObservableProperty]
    private GameStatsDto? _stats;

    [ObservableProperty]
    private string _totalDownloadsText = "0";

    [ObservableProperty]
    private string _last30Text = "0";

    [ObservableProperty]
    private string _last7Text = "0";

    [ObservableProperty]
    private string _lastDownloadText = "—";

    [ObservableProperty]
    private bool _hasDailyBars;

    [ObservableProperty]
    private bool _hasVersions;

    public bool HasSelection => SelectedGame is not null;
    public bool HasHeroPreview => HeroPreview is not null;
    public bool IsGamesTab => SelectedTab == "games";
    public bool IsFeaturedTab => SelectedTab == "featured";
    public bool IsUsersTab => SelectedTab == "users";
    public bool IsOverviewTab => EditorTab == "overview";
    public bool IsStoreTab => EditorTab == "store";
    public bool IsMediaTab => EditorTab == "media";
    public bool IsBuildsTab => EditorTab == "builds";
    public bool IsDlcTab => EditorTab == "dlc";

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

    public void SetAddVersionHandler(Action add) => _addVersion = add;
    public void SetAddDlcHandler(Action<string> add) => _addDlc = add;
    public void SetConfirmDeleteHandler(Func<AdminGameItem, Task<bool>> confirm) => _confirmDelete = confirm;
    public void SetPickImageHandler(Func<Task<string?>> pick) => _pickImage = pick;
    public void SetPickVideoHandler(Func<Task<string?>> pick) => _pickVideo = pick;

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

    partial void OnEditorTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverviewTab));
        OnPropertyChanged(nameof(IsStoreTab));
        OnPropertyChanged(nameof(IsMediaTab));
        OnPropertyChanged(nameof(IsBuildsTab));
        OnPropertyChanged(nameof(IsDlcTab));
    }

    partial void OnShowEmptyChanged(bool value) => NotifyPanels();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedGameChanged(AdminGameItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedPublished));
        OnPropertyChanged(nameof(SelectedFeatured));
        _ = LoadSelectionExtrasAsync();
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

            foreach (var game in Games.Where(g => !g.IsDlc))
            {
                foreach (var dlc in Games.Where(g => g.IsDlc && g.ParentGameId == game.Id))
                {
                    game.Dlcs.Add(dlc);
                }
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
    private void SelectEditorTab(string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            EditorTab = tag;
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
    private void AddVersion() => _addVersion?.Invoke();

    [RelayCommand]
    private void AddDlc(AdminGameItem? parent)
    {
        var target = parent ?? SelectedGame;
        if (target is null || string.IsNullOrWhiteSpace(target.Symbol) || target.IsDlc)
        {
            return;
        }

        _addDlc?.Invoke(target.Symbol);
    }

    [RelayCommand]
    private async Task SaveStoreAsync()
    {
        if (SelectedGame is null || _api is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var item = SelectedGame;
            var updated = await _api.UpdateAdminGameAsync(item.Id, item.ToUpdateRequest());
            if (updated is null)
            {
                StatusText = "Nie udało się zapisać strony sklepu.";
                return;
            }

            item.ApplyDto(updated, _apiBaseUrl);
            _ = item.LoadCoverAsync();
            StatusText = $"Zapisano stronę sklepu: {updated.Title}";
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd zapisu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PickCoverAsync()
    {
        if (_pickImage is null)
        {
            return;
        }

        var path = await _pickImage();
        if (path is null || SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.UploadGameCoverAsync(SelectedGame.Id, path, asAdmin: true);
        await ReloadAfterMediaAsync(updated, "Zaktualizowano miniaturkę.");
    }

    [RelayCommand]
    private async Task ClearCoverAsync()
    {
        if (SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.ClearGameCoverAsync(SelectedGame.Id, asAdmin: true);
        await ReloadAfterMediaAsync(updated, "Usunięto miniaturkę.");
    }

    [RelayCommand]
    private async Task PickHeaderAsync()
    {
        if (_pickImage is null)
        {
            return;
        }

        var path = await _pickImage();
        if (path is null || SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.UploadGameHeaderAsync(SelectedGame.Id, path, asAdmin: true);
        await ReloadAfterMediaAsync(updated, "Zaktualizowano okładkę poziomą.");
    }

    [RelayCommand]
    private async Task ClearHeaderAsync()
    {
        if (SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.ClearGameHeaderAsync(SelectedGame.Id, asAdmin: true);
        await ReloadAfterMediaAsync(updated, "Usunięto okładkę poziomą.");
    }

    [RelayCommand]
    private async Task PickIntroAsync()
    {
        if (_pickVideo is null)
        {
            return;
        }

        var path = await _pickVideo();
        if (path is null || SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.UploadGameIntroAsync(SelectedGame.Id, path, asAdmin: true);
        await ReloadAfterMediaAsync(updated, "Zaktualizowano intro.");
    }

    [RelayCommand]
    private async Task ClearIntroAsync()
    {
        if (SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.ClearGameIntroAsync(SelectedGame.Id, asAdmin: true);
        await ReloadAfterMediaAsync(updated, "Usunięto intro.");
    }

    [RelayCommand]
    private async Task AddScreenshotAsync()
    {
        if (_pickImage is null || SelectedGame is null || _api is null)
        {
            return;
        }

        var path = await _pickImage();
        if (path is null)
        {
            return;
        }

        var updated = await _api.AddGameScreenshotAsync(SelectedGame.Id, path, asAdmin: true);
        await ReloadAfterMediaAsync(updated, "Dodano zrzut ekranu.");
    }

    [RelayCommand]
    private async Task RemoveScreenshotAsync(StudioScreenshotItem? shot)
    {
        if (shot is null || SelectedGame is null || _api is null || shot.Id <= 0)
        {
            return;
        }

        var updated = await _api.RemoveGameScreenshotAsync(SelectedGame.Id, shot.Id, asAdmin: true);
        await ReloadAfterMediaAsync(updated, "Usunięto zrzut ekranu.");
    }

    private async Task ReloadAfterMediaAsync(GameDto? updated, string okText)
    {
        if (updated is null)
        {
            StatusText = "Nie udało się zaktualizować mediów.";
            return;
        }

        SelectedGame?.ApplyDto(updated, _apiBaseUrl);
        if (SelectedGame is not null)
        {
            _ = SelectedGame.LoadCoverAsync();
        }

        StatusText = okText;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveVersionNotesAsync(StudioVersionItem? version)
    {
        if (version is null || SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.UpdateAdminVersionNotesAsync(SelectedGame.Id, version.Id, version.EditNotes);
        if (updated is null)
        {
            StatusText = "Nie udało się zapisać opisu zmian.";
            return;
        }

        version.ApplyDto(updated);
        StatusText = $"Zapisano opis zmian dla {updated.Version}.";
    }

    private async Task LoadSelectionExtrasAsync()
    {
        Versions.Clear();
        DailyBars.Clear();
        HasVersions = false;
        HasDailyBars = false;
        Stats = null;
        TotalDownloadsText = "0";
        Last30Text = "0";
        Last7Text = "0";
        LastDownloadText = "—";

        if (SelectedGame is null || _api is null)
        {
            return;
        }

        var gameId = SelectedGame.Id;
        try
        {
            var statsTask = _api.GetAdminGameStatsAsync(gameId);
            var versionsTask = _api.GetAdminVersionsAsync(gameId);
            await Task.WhenAll(statsTask, versionsTask);
            if (SelectedGame?.Id != gameId)
            {
                return;
            }

            ApplyStats(statsTask.Result);
            foreach (var version in versionsTask.Result)
            {
                Versions.Add(StudioVersionItem.FromDto(version));
            }

            HasVersions = Versions.Count > 0;
        }
        catch
        {
            // keep empty extras
        }
    }

    private void ApplyStats(GameStatsDto? stats)
    {
        Stats = stats;
        TotalDownloadsText = (stats?.TotalDownloads ?? 0).ToString("N0", Polish);
        Last30Text = (stats?.DownloadsLast30Days ?? 0).ToString("N0", Polish);
        Last7Text = (stats?.DownloadsLast7Days ?? 0).ToString("N0", Polish);
        LastDownloadText = stats?.LastDownloadUtc is { } last
            ? last.LocalDateTime.ToString("d MMM yyyy HH:mm", Polish)
            : "brak";

        var max = stats?.Daily.Max(p => p.Downloads) ?? 0;
        DailyBars.Clear();
        if (stats is null || stats.Daily.Count == 0)
        {
            HasDailyBars = false;
            return;
        }

        foreach (var point in stats.Daily)
        {
            DailyBars.Add(new StudioStatBar
            {
                Label = point.Day.ToString("d.MM"),
                Value = point.Downloads,
                Max = Math.Max(1, max)
            });
        }

        HasDailyBars = DailyBars.Count > 0;
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

        item.ApplyDto(updated, _apiBaseUrl);
        _ = item.LoadCoverAsync();
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

        item.ApplyDto(updated, _apiBaseUrl);
        _ = item.LoadCoverAsync();
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
                    ordered[i].ApplyDto(updated, _apiBaseUrl);
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
    public required string Title { get; set; }
    public required string Symbol { get; init; }
    public bool IsDlc { get; init; }
    public int? ParentGameId { get; init; }
    public string? ParentSymbol { get; init; }
    public string? OwnerDisplayName { get; init; }
    public string? OwnerEmail { get; init; }
    public ObservableCollection<AdminGameItem> Dlcs { get; } = [];
    public ObservableCollection<StudioScreenshotItem> ScreenshotItems { get; } = [];
    public ObservableCollection<StudioBuildItem> Builds { get; } = [];

    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial string? HardwareRequirements { get; set; }
    [ObservableProperty] public partial string? OtherInformation { get; set; }
    [ObservableProperty] public partial string? GenreSymbol { get; set; }
    [ObservableProperty] public partial string? EngineSymbol { get; set; }
    [ObservableProperty] public partial string? CoverUrl { get; set; }
    [ObservableProperty] public partial string? HeaderUrl { get; set; }
    [ObservableProperty] public partial string? IntroUrl { get; set; }
    [ObservableProperty] public partial string? CoverImageUrl { get; set; }
    [ObservableProperty] public partial string? HeaderImageUrl { get; set; }
    [ObservableProperty] public partial string? OwnCoverImageUrl { get; set; }
    [ObservableProperty] public partial string? OwnHeaderImageUrl { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateLabel))]
    [NotifyPropertyChangedFor(nameof(MetaText))]
    [NotifyPropertyChangedFor(nameof(IsPublished))]
    private bool _draftP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MetaText))]
    private bool _featuredP;

    [ObservableProperty] private int? _featuredOrder;
    [ObservableProperty] private string _positionLabel = string.Empty;
    [ObservableProperty] private Bitmap? _coverBitmap;
    [ObservableProperty] private bool _hasCover;
    [ObservableProperty] public partial string CurrentVersionText { get; set; } = "—";
    [ObservableProperty] public partial string EditTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditHardwareRequirements { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditOtherInformation { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditGenre { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditEngine { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditStudioName { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditWebsiteUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDiscordUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditSupportUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial int EditReleaseStatusIndex { get; set; } = 2;
    [ObservableProperty] public partial DateTimeOffset? EditReleaseDate { get; set; }
    [ObservableProperty] public partial int EditAgeRatingIndex { get; set; }
    [ObservableProperty] public partial string EditLanguages { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditTags { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasSinglePlayer { get; set; }
    [ObservableProperty] public partial bool HasMultiPlayer { get; set; }
    [ObservableProperty] public partial bool HasCoOp { get; set; }
    [ObservableProperty] public partial bool HasController { get; set; }
    [ObservableProperty] public partial bool HasCloudSaves { get; set; }
    [ObservableProperty] public partial bool HasAchievements { get; set; }
    [ObservableProperty] public partial bool HasModding { get; set; }
    [ObservableProperty] public partial bool HasVr { get; set; }

    public bool HasIntro => !string.IsNullOrWhiteSpace(IntroUrl);
    public bool HasOwnCover => !string.IsNullOrWhiteSpace(OwnCoverImageUrl);
    public bool HasOwnHeader => !string.IsNullOrWhiteSpace(OwnHeaderImageUrl);
    public bool HasHeaderArt => !string.IsNullOrWhiteSpace(HeaderImageUrl);
    public bool HasDlcs => Dlcs.Count > 0;
    public bool IsPublished => !DraftP;
    public string KindLabel => IsDlc ? "DLC" : "Gra";
    public string StateLabel => DraftP ? "Szkic" : "Opublikowana";
    public string OwnerText => string.IsNullOrWhiteSpace(OwnerDisplayName)
        ? (string.IsNullOrWhiteSpace(OwnerEmail) ? "—" : OwnerEmail!)
        : OwnerDisplayName!;
    public string MetaText => $"{Symbol} · {KindLabel} · {StateLabel}";
    public string TileLabel => string.IsNullOrWhiteSpace(Symbol)
        ? Title[..Math.Min(2, Title.Length)].ToUpperInvariant()
        : Symbol[..Math.Min(3, Symbol.Length)].ToUpperInvariant();

    public GameFeatures EditedFeatures
    {
        get
        {
            var value = GameFeatures.None;
            if (HasSinglePlayer) value |= GameFeatures.SinglePlayer;
            if (HasMultiPlayer) value |= GameFeatures.MultiPlayer;
            if (HasCoOp) value |= GameFeatures.CoOp;
            if (HasController) value |= GameFeatures.ControllerSupport;
            if (HasCloudSaves) value |= GameFeatures.CloudSaves;
            if (HasAchievements) value |= GameFeatures.Achievements;
            if (HasModding) value |= GameFeatures.Modding;
            if (HasVr) value |= GameFeatures.Vr;
            return value;
        }
    }

    public UpdateGameRequestDto ToUpdateRequest() => new()
    {
        Title = string.IsNullOrWhiteSpace(EditTitle) ? Title : EditTitle.Trim(),
        Description = EditDescription,
        HardwareRequirements = EditHardwareRequirements,
        OtherInformation = EditOtherInformation,
        Genre = EditGenre,
        Engine = EditEngine,
        DraftP = DraftP,
        StudioName = EditStudioName,
        WebsiteUrl = EditWebsiteUrl,
        DiscordUrl = EditDiscordUrl,
        SupportUrl = EditSupportUrl,
        ReleaseStatus = GameStoreLabels.ReleaseStatusFromIndex(EditReleaseStatusIndex),
        ReleaseDateUtc = EditReleaseDate,
        ClearReleaseDate = EditReleaseDate is null,
        AgeRating = GameStoreLabels.AgeRatingFromIndex(EditAgeRatingIndex),
        Features = EditedFeatures,
        Languages = CsvList.Split(EditLanguages),
        Tags = CsvList.Split(EditTags)
    };

    public static AdminGameItem FromDto(GameDto game, string apiBaseUrl)
    {
        var item = new AdminGameItem
        {
            Id = game.Id,
            Title = game.Title,
            Symbol = game.Symbol,
            IsDlc = game.Kind == GameContentKind.Dlc,
            ParentGameId = game.ParentGameId,
            ParentSymbol = game.ParentSymbol,
            OwnerDisplayName = game.OwnerDisplayName,
            OwnerEmail = game.OwnerEmail
        };
        item.ApplyDto(game, apiBaseUrl);
        return item;
    }

    public void ApplyDto(GameDto game, string? apiBaseUrl = null)
    {
        Title = game.Title;
        Description = game.Description;
        HardwareRequirements = game.HardwareRequirements;
        OtherInformation = game.OtherInformation;
        GenreSymbol = game.GenreSymbol;
        EngineSymbol = game.EngineSymbol;
        CoverUrl = game.CoverUrl;
        HeaderUrl = game.HeaderUrl;
        IntroUrl = game.IntroUrl;
        DraftP = game.DraftP;
        FeaturedP = game.FeaturedP;
        FeaturedOrder = game.FeaturedOrder;
        CurrentVersionText = game.Builds.FirstOrDefault()?.CurrentVersion ?? "—";

        if (apiBaseUrl is not null)
        {
            CoverImageUrl = ResolveMedia(game.ThumbnailUrl, apiBaseUrl);
            HeaderImageUrl = ResolveMedia(game.LandscapeUrl, apiBaseUrl);
            OwnCoverImageUrl = ResolveMedia(game.CoverUrl, apiBaseUrl);
            OwnHeaderImageUrl = ResolveMedia(game.HeaderUrl, apiBaseUrl);
        }

        EditTitle = game.Title;
        EditDescription = game.Description ?? string.Empty;
        EditHardwareRequirements = game.HardwareRequirements ?? string.Empty;
        EditOtherInformation = game.OtherInformation ?? string.Empty;
        EditGenre = game.GenreSymbol ?? string.Empty;
        EditEngine = game.EngineSymbol ?? string.Empty;
        EditStudioName = game.StudioName ?? string.Empty;
        EditWebsiteUrl = game.WebsiteUrl ?? string.Empty;
        EditDiscordUrl = game.DiscordUrl ?? string.Empty;
        EditSupportUrl = game.SupportUrl ?? string.Empty;
        EditReleaseStatusIndex = GameStoreLabels.ReleaseStatusIndex(game.ReleaseStatus);
        EditReleaseDate = game.ReleaseDateUtc;
        EditAgeRatingIndex = GameStoreLabels.AgeRatingIndex(game.AgeRating);
        EditLanguages = string.Join(", ", game.Languages);
        EditTags = string.Join(", ", game.Tags);
        HasSinglePlayer = game.Features.HasFlag(GameFeatures.SinglePlayer);
        HasMultiPlayer = game.Features.HasFlag(GameFeatures.MultiPlayer);
        HasCoOp = game.Features.HasFlag(GameFeatures.CoOp);
        HasController = game.Features.HasFlag(GameFeatures.ControllerSupport);
        HasCloudSaves = game.Features.HasFlag(GameFeatures.CloudSaves);
        HasAchievements = game.Features.HasFlag(GameFeatures.Achievements);
        HasModding = game.Features.HasFlag(GameFeatures.Modding);
        HasVr = game.Features.HasFlag(GameFeatures.Vr);

        if (apiBaseUrl is not null)
        {
            ScreenshotItems.Clear();
            foreach (var shot in game.Screenshots ?? [])
            {
                ScreenshotItems.Add(new StudioScreenshotItem
                {
                    Id = shot.Id,
                    ImageUrl = InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, shot.Url)
                });
            }

            Builds.Clear();
            foreach (var build in game.Builds)
            {
                Builds.Add(new StudioBuildItem
                {
                    PlatformText = $"{build.Platform} {build.Architecture}".Trim(),
                    Version = build.CurrentVersion,
                    ExeName = build.ExeName,
                    IsDefault = build.IsDefault
                });
            }
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsPublished));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(HasDlcs));
        OnPropertyChanged(nameof(OwnerText));
        OnPropertyChanged(nameof(HasIntro));
        OnPropertyChanged(nameof(HasOwnCover));
        OnPropertyChanged(nameof(HasOwnHeader));
        OnPropertyChanged(nameof(HasHeaderArt));
    }

    private static string? ResolveMedia(string? url, string apiBaseUrl) =>
        string.IsNullOrWhiteSpace(url) ? null : InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, url);

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
