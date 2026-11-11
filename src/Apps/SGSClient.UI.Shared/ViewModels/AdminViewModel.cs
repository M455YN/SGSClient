using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;

namespace SGSClient.UI.ViewModels;

public partial class AdminViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl;
    private readonly Dictionary<string, CreativeWorksViewModel> _works = [];
    private Action<int>? _addVersion;
    private Func<string, string, Task<string?>>? _saveFile;
    private Action<string>? _addDlc;
    private Func<AdminGameItem, Task<bool>>? _confirmDelete;
    private Func<Task<string?>>? _pickImage;
    private Func<Task<string?>>? _pickVideo;

    public AdminViewModel()
        : this(null, null)
    {
    }

    public AdminViewModel(ISgsApiService? api, IAuthSession? auth, IConfiguration? configuration = null)
    {
        _api = api;
        _auth = auth;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = Loc.T("Ładowanie panelu admina…");
        foreach (var section in new[] { "art", "music", "stories" })
        {
            _works[section] = new CreativeWorksViewModel(api, _apiBaseUrl, section, CreativeWorksMode.Admin);
        }
        AchievementsEditor = new AchievementsEditorViewModel(api, _apiBaseUrl, text => StatusText = text, busy => IsBusy = busy);
        AchievementsEditor.Saved += hasAny =>
        {
            if (SelectedGame is not null)
            {
                SelectedGame.HasAchievements = hasAny;
            }
        };
    }

    public string Title { get; } = "Admin";
    public AchievementsEditorViewModel AchievementsEditor { get; }
    public IReadOnlyList<string> ReleaseStatusOptions { get; } = GameStoreLabels.ReleaseStatusOptions;
    public IReadOnlyList<string> AgeRatingOptions { get; } = GameStoreLabels.AgeRatingOptions;

    public ObservableCollection<AdminGameItem> Games { get; } = [];
    public ObservableCollection<AdminGameItem> FilteredGames { get; } = [];
    public ObservableCollection<AdminGameItem> FeaturedGames { get; } = [];
    public ObservableCollection<AdminUserItem> Users { get; } = [];
    public ObservableCollection<StudioVersionItem> Versions { get; } = [];
    public ObservableCollection<StudioStatBar> DailyBars { get; } = [];

    [ObservableProperty] public partial string SelectedTab { get; set; } = "games";
    [ObservableProperty] public partial string EditorTab { get; set; } = "overview";
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool ShowEmpty { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string Filter { get; set; } = "all";
    [ObservableProperty] public partial GameStatsDto? Stats { get; set; }
    [ObservableProperty] public partial string TotalDownloadsText { get; set; } = "0";
    [ObservableProperty] public partial string Last30Text { get; set; } = "0";
    [ObservableProperty] public partial string Last7Text { get; set; } = "0";
    [ObservableProperty] public partial string LastDownloadText { get; set; } = "—";
    [ObservableProperty] public partial bool HasDailyBars { get; set; }
    [ObservableProperty] public partial bool HasVersions { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial AdminGameItem? SelectedGame { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHeroPreview))]
    public partial AdminGameItem? HeroPreview { get; set; }

    [ObservableProperty] public partial bool HasFeatured { get; set; }
    [ObservableProperty] public partial string ListSummary { get; set; } = string.Empty;
    [ObservableProperty] public partial string FeaturedSummary { get; set; } = string.Empty;

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
    public bool IsAchievementsTab => EditorTab == "achievements";
    /// <summary>Tabs art | music | stories show the shared works editor in admin mode.</summary>
    public bool IsWorksTab => _works.ContainsKey(SelectedTab);
    public CreativeWorksViewModel? CurrentWorks => _works.GetValueOrDefault(SelectedTab);
    public IEnumerable<CreativeWorksViewModel> AllWorks => _works.Values;
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

    public void SetAddVersionHandler(Action<int> add) => _addVersion = add;
    public void SetSaveFileHandler(Func<string, string, Task<string?>> save) => _saveFile = save;
    public void SetAddDlcHandler(Action<string> add) => _addDlc = add;
    public void SetConfirmDeleteHandler(Func<AdminGameItem, Task<bool>> confirm) => _confirmDelete = confirm;
    public void SetPickImageHandler(Func<Task<string?>> pick)
    {
        _pickImage = pick;
        AchievementsEditor.SetPickImageHandler(pick);
    }

    public void SetPickVideoHandler(Func<Task<string?>> pick) => _pickVideo = pick;

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsGamesTab));
        OnPropertyChanged(nameof(IsFeaturedTab));
        OnPropertyChanged(nameof(IsUsersTab));
        OnPropertyChanged(nameof(IsWorksTab));
        OnPropertyChanged(nameof(CurrentWorks));
        ShowEmpty = value switch
        {
            "games" => FilteredGames.Count == 0,
            "featured" => FeaturedGames.Count == 0,
            "users" => Users.Count == 0,
            _ => false
        };
        if (CurrentWorks is { IsLoaded: false } works)
        {
            _ = works.LoadAsync();
        }
        NotifyPanels();
    }

    partial void OnShowEmptyChanged(bool value) => NotifyPanels();

    partial void OnEditorTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverviewTab));
        OnPropertyChanged(nameof(IsStoreTab));
        OnPropertyChanged(nameof(IsMediaTab));
        OnPropertyChanged(nameof(IsBuildsTab));
        OnPropertyChanged(nameof(IsDlcTab));
        OnPropertyChanged(nameof(IsAchievementsTab));
        if (value == "achievements" && SelectedGame is not null && AchievementsEditor.Items.Count == 0)
        {
            _ = AchievementsEditor.LoadAsync(SelectedGame.Id);
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedGameChanged(AdminGameItem? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedPublished));
        OnPropertyChanged(nameof(SelectedFeatured));
        AchievementsEditor.Clear();
        if (value is not null && IsAchievementsTab)
        {
            _ = AchievementsEditor.LoadAsync(value.Id);
        }
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
            StatusText = Loc.T("API niedostępne.");
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
            StatusText = Loc.T("Zaloguj się jako admin (Profil), aby zarządzać katalogiem.");
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
                ? Loc.T("Brak danych lub brak uprawnień admina.")
                : Loc.F("{0} pozycji katalogu · {1} użytkowników", Games.Count, Users.Count);
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Błąd: {0}", ex.Message);
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
            StatusText = Loc.T("Brak połączenia z API.");
            item.OnRoleReverted();
            return;
        }

        if (role == item.Role)
        {
            return;
        }

        var previous = item.Role;
        item.Role = role;
        IsBusy = true;
        try
        {
            var result = await _api.UpdateAdminUserRoleAsync(item.Id, role);
            if (!result.Success || result.User is null)
            {
                item.Role = previous;
                StatusText = result.Error ?? Loc.T("Nie udało się zmienić roli.");
                item.OnRoleReverted();
                return;
            }

            item.ApplyDto(result.User);
            ApplyRolePermissions(item);
            StatusText = $"{item.DisplayName}: rola {AdminRoles.Label(item.Role).ToLowerInvariant()}";
        }
        catch (Exception ex)
        {
            item.Role = previous;
            item.OnRoleReverted();
            StatusText = Loc.F("Błąd zmiany roli: {0}", ex.Message);
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
        ShowEmpty = !IsWorksTab && (IsGamesTab
            ? FilteredGames.Count == 0
            : IsFeaturedTab ? FeaturedGames.Count == 0 : Users.Count == 0);
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
            ? Loc.F("{0} wyróżnionych · pierwsza pozycja trafia na hero", FeaturedGames.Count)
            : Loc.T("Brak wyróżnionych gier. Włącz przełącznik przy grze na liście.");
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
    private void SelectGame(AdminGameItem? item)
    {
        if (item is not null)
        {
            SelectedGame = item;
        }
    }

    [RelayCommand]
    private void AddVersion()
    {
        if (SelectedGame is not null)
        {
            _addVersion?.Invoke(SelectedGame.Id);
        }
    }

    [RelayCommand]
    private async Task DownloadManifestAsync()
    {
        if (SelectedGame is null || _api is null || _saveFile is null)
        {
            return;
        }

        StatusText = await GameManifestSource.SaveAsync(_api, SelectedGame.Id, asAdmin: true, _saveFile, _apiBaseUrl)
                     ?? StatusText;
    }

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
                StatusText = Loc.T("Nie udało się zapisać strony sklepu.");
                return;
            }

            item.ApplyDto(updated, _apiBaseUrl);
            StatusText = Loc.F("Zapisano stronę sklepu: {0}", updated.Title);
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Błąd zapisu: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task PickCoverAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameCoverAsync(id, path, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Zaktualizowano miniaturkę."));

    [RelayCommand]
    private Task ClearCoverAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameCoverAsync(id, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Usunięto miniaturkę."));

    [RelayCommand]
    private Task PickHeaderAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameHeaderAsync(id, path, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Zaktualizowano okładkę poziomą."));

    [RelayCommand]
    private Task ClearHeaderAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameHeaderAsync(id, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Usunięto okładkę poziomą."));

    [RelayCommand]
    private Task PickLogoAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameLogoAsync(id, path, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Zaktualizowano logo."));

    [RelayCommand]
    private Task ClearLogoAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameLogoAsync(id, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Usunięto logo."));

    [RelayCommand]
    private Task PickIntroAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickVideo,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameIntroAsync(id, path, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Zaktualizowano intro."));

    [RelayCommand]
    private Task ClearIntroAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameIntroAsync(id, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Usunięto intro."));

    [RelayCommand]
    private Task AddScreenshotAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.AddGameScreenshotAsync(id, path, asAdmin: true),
                ReloadAfterMediaAsync,
                Loc.T("Dodano zrzut ekranu."));

    [RelayCommand]
    private async Task RemoveScreenshotAsync(StudioScreenshotItem? shot)
    {
        if (shot is null || SelectedGame is null || _api is null || shot.Id <= 0)
        {
            return;
        }

        var updated = await _api.RemoveGameScreenshotAsync(SelectedGame.Id, shot.Id, asAdmin: true);
        await ReloadAfterMediaAsync(updated, Loc.T("Usunięto zrzut ekranu."));
    }

    private Task ReloadAfterMediaAsync(GameDto? updated, string okText)
    {
        if (updated is null)
        {
            StatusText = okText.StartsWith(Loc.T("Błąd"), StringComparison.OrdinalIgnoreCase)
                ? okText
                : Loc.T("Nie udało się zaktualizować mediów (serwer odrzucił plik lub brak uprawnień).");
            return Task.CompletedTask;
        }

        var editor = EditorTab;
        SelectedGame?.ApplyDto(updated, _apiBaseUrl);
        var listed = Games.FirstOrDefault(g => g.Id == updated.Id);
        if (listed is not null && !ReferenceEquals(listed, SelectedGame))
        {
            listed.ApplyDto(updated, _apiBaseUrl);
        }

        StatusText = okText;
        EditorTab = editor;
        return Task.CompletedTask;
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
            StatusText = Loc.T("Nie udało się zapisać opisu zmian.");
            return;
        }

        version.ApplyDto(updated);
        StatusText = Loc.F("Zapisano opis zmian dla {0}.", updated.Version);
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

            BindStats(statsTask.Result);
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

    private void BindStats(GameStatsDto? stats)
    {
        Stats = stats;
        GameEditorStats.Apply(
            stats,
            DailyBars,
            out var total,
            out var last30,
            out var last7,
            out var lastDownload,
            out var hasBars);
        TotalDownloadsText = total;
        Last30Text = last30;
        Last7Text = last7;
        LastDownloadText = lastDownload;
        HasDailyBars = hasBars;
    }

    [RelayCommand]
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
            StatusText = Loc.T("Nie udało się zmienić statusu publikacji.");
            item.DraftP = !draft;
            OnPropertyChanged(nameof(SelectedPublished));
            return;
        }

        item.ApplyDto(updated, _apiBaseUrl);
        ApplyFilter();
        OnPropertyChanged(nameof(SelectedPublished));
        StatusText = updated.DraftP
            ? Loc.F("Ukryto w sklepie: {0}", updated.Title)
            : $"Opublikowano: {updated.Title}";
    }

    [RelayCommand]
    private async Task SetFeaturedAsync(bool featured)
    {
        var item = SelectedGame;
        if (item is null || _api is null || item.FeaturedP == featured)
        {
            return;
        }

        var order = featured ? FeaturedGames.Count : -1;
        var updated = await _api.UpdateAdminGameAsync(item.Id, new UpdateGameRequestDto
        {
            FeaturedP = featured,
            FeaturedOrder = order
        });

        if (updated is null)
        {
            StatusText = Loc.T("Nie udało się zmienić wyróżnienia.");
            item.FeaturedP = !featured;
            OnPropertyChanged(nameof(SelectedFeatured));
            return;
        }

        item.ApplyDto(updated, _apiBaseUrl);
        ApplyFilter();
        RebuildFeatured();
        OnPropertyChanged(nameof(SelectedFeatured));
        StatusText = updated.FeaturedP
            ? Loc.F("Wyróżniono: {0}", updated.Title)
            : Loc.F("Usunięto wyróżnienie: {0}", updated.Title);
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
            }

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].PositionLabel = i == 0 ? "Hero" : $"#{i + 1}";
            }

            HeroPreview = FeaturedGames.FirstOrDefault();
            StatusText = Loc.T("Zapisano kolejność wyróżnionych.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się zapisać kolejności: {0}", ex.Message);
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
                ? Loc.F("Usunięto: {0}", item.Title)
                : Loc.T("Nie udało się usunąć (sprawdź powiązane DLC).");
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
