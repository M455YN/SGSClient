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

public partial class StudioViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl;
    private Action? _addGame;
    private Action<int>? _addVersion;
    private Func<string, string, Task<string?>>? _saveFile;
    private Action<string>? _addDlc;
    private Func<StudioContentItem, Task<bool>>? _confirmDelete;
    private Func<Task<string?>>? _pickImage;
    private Func<Task<string?>>? _pickVideo;
    private int _selectedId;
    private readonly Dictionary<string, CreativeWorksViewModel> _works = [];

    public StudioViewModel()
        : this(null, null)
    {
    }

    public StudioViewModel(ISgsApiService? api, IAuthSession? auth, IConfiguration? configuration = null)
    {
        _api = api;
        _auth = auth;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = Loc.T("Ładowanie Twoich gier…");
        foreach (var section in new[] { "art", "music", "stories" })
        {
            _works[section] = new CreativeWorksViewModel(api, _apiBaseUrl, section, CreativeWorksMode.Mine);
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

    public string Title { get; } = Loc.T("Panel twórcy");
    public AchievementsEditorViewModel AchievementsEditor { get; }
    public IReadOnlyList<string> ReleaseStatusOptions { get; } = GameStoreLabels.ReleaseStatusOptions;
    public IReadOnlyList<string> AgeRatingOptions { get; } = GameStoreLabels.AgeRatingOptions;

    public ObservableCollection<StudioContentItem> Games { get; } = [];
    public ObservableCollection<StudioContentItem> FilteredGames { get; } = [];
    public ObservableCollection<StudioVersionItem> Versions { get; } = [];
    public ObservableCollection<StudioStatBar> DailyBars { get; } = [];

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial bool HasGames { get; set; }
    [ObservableProperty] public partial bool ShowEmpty { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string SelectedTab { get; set; } = "overview";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial StudioContentItem? SelectedGame { get; set; }

    [ObservableProperty] public partial GameStatsDto? Stats { get; set; }
    [ObservableProperty] public partial string TotalDownloadsText { get; set; } = "0";
    [ObservableProperty] public partial string Last30Text { get; set; } = "0";
    [ObservableProperty] public partial string Last7Text { get; set; } = "0";
    [ObservableProperty] public partial string LastDownloadText { get; set; } = "—";
    [ObservableProperty] public partial bool HasDailyBars { get; set; }
    [ObservableProperty] public partial bool HasVersions { get; set; }
    [ObservableProperty] public partial string ListSummary { get; set; } = string.Empty;

    /// <summary>games | art | music | stories - the top-level section of the panel.</summary>
    [ObservableProperty] public partial string Section { get; set; } = "games";

    public bool IsGamesSection => Section == "games";
    public bool IsWorksSection => Section != "games";
    public CreativeWorksViewModel? CurrentWorks => _works.GetValueOrDefault(Section);
    public IEnumerable<CreativeWorksViewModel> AllWorks => _works.Values;

    [RelayCommand]
    private void SelectSection(string? section)
    {
        if (!string.IsNullOrWhiteSpace(section))
        {
            Section = section;
        }
    }

    partial void OnSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsGamesSection));
        OnPropertyChanged(nameof(IsWorksSection));
        OnPropertyChanged(nameof(CurrentWorks));
        if (CurrentWorks is { IsLoaded: false } works)
        {
            _ = works.LoadAsync();
        }
    }

    public bool HasSelection => SelectedGame is not null;
    public bool IsOverviewTab => SelectedTab == "overview";
    public bool IsStoreTab => SelectedTab == "store";
    public bool IsMediaTab => SelectedTab == "media";
    public bool IsBuildsTab => SelectedTab == "builds";
    public bool IsDlcTab => SelectedTab == "dlc";
    public bool IsAchievementsTab => SelectedTab == "achievements";

    public void SetAddGameHandler(Action add) => _addGame = add;
    public void SetAddVersionHandler(Action<int> add) => _addVersion = add;
    public void SetSaveFileHandler(Func<string, string, Task<string?>> save) => _saveFile = save;
    public void SetAddDlcHandler(Action<string> add) => _addDlc = add;
    public void SetConfirmDeleteHandler(Func<StudioContentItem, Task<bool>> confirm) => _confirmDelete = confirm;
    public void SetPickImageHandler(Func<Task<string?>> pick)
    {
        _pickImage = pick;
        AchievementsEditor.SetPickImageHandler(pick);
    }
    public void SetPickVideoHandler(Func<Task<string?>> pick) => _pickVideo = pick;

    partial void OnSelectedTabChanged(string value)
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

    partial void OnSelectedGameChanged(StudioContentItem? value)
    {
        _selectedId = value?.Id ?? 0;
        OnPropertyChanged(nameof(HasSelection));
        AchievementsEditor.Clear();
        if (value is not null && IsAchievementsTab)
        {
            _ = AchievementsEditor.LoadAsync(value.Id);
        }

        _ = LoadSelectionExtrasAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (_api is null)
        {
            StatusText = Loc.T("API niedostępne.");
            ShowEmpty = true;
            HasGames = false;
            return;
        }

        if (_auth?.IsAvailable == true && !_auth.IsLoggedIn)
        {
            Games.Clear();
            FilteredGames.Clear();
            SelectedGame = null;
            HasGames = false;
            ShowEmpty = true;
            StatusText = Loc.T("Zaloguj się w Profilu, aby zobaczyć swoje gry.");
            return;
        }

        IsBusy = true;
        try
        {
            var all = await _api.GetMyGamesAsync();
            var baseGames = all.Where(g => g.Kind != GameContentKind.Dlc).OrderBy(g => g.Title).ToList();
            var dlcByParent = all
                .Where(g => g.Kind == GameContentKind.Dlc && g.ParentGameId is not null)
                .GroupBy(g => g.ParentGameId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            Games.Clear();
            foreach (var game in baseGames)
            {
                var item = StudioContentItem.FromGame(game, _apiBaseUrl);
                if (game.Dlcs.Count > 0)
                {
                    foreach (var dlc in game.Dlcs.OrderBy(d => d.Title))
                    {
                        item.Dlcs.Add(StudioContentItem.FromDlc(dlc, game.Symbol, game.DraftP, _apiBaseUrl));
                    }
                }
                else if (dlcByParent.TryGetValue(game.Id, out var ownedDlcs))
                {
                    foreach (var dlc in ownedDlcs.OrderBy(d => d.Title))
                    {
                        item.Dlcs.Add(StudioContentItem.FromGame(dlc, _apiBaseUrl));
                    }
                }

                Games.Add(item);
            }

            ApplyFilter();
            var keep = Games.FirstOrDefault(g => g.Id == _selectedId) ?? Games.FirstOrDefault();
            SelectedGame = keep;
            HasGames = Games.Count > 0;
            ShowEmpty = !HasGames;
            StatusText = HasGames
                ? Loc.F("{0} gier · {1} DLC", Games.Count, Games.Sum(g => g.Dlcs.Count))
                : Loc.T("Nie masz jeszcze opublikowanych gier. Dodaj pierwszą.");
        }
        catch (Exception ex)
        {
            HasGames = false;
            ShowEmpty = true;
            StatusText = Loc.F("Błąd ładowania: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        IEnumerable<StudioContentItem> source = Games;
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(g =>
                g.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || g.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var selectedId = SelectedGame?.Id;
        FilteredGames.Clear();
        foreach (var item in source)
        {
            FilteredGames.Add(item);
        }

        ListSummary = FilteredGames.Count == Games.Count
            ? $"{Games.Count} gier"
            : $"{FilteredGames.Count} z {Games.Count}";

        if (selectedId is int id && FilteredGames.All(g => g.Id != id))
        {
            SelectedGame = FilteredGames.FirstOrDefault();
        }

        ShowEmpty = Games.Count == 0 || FilteredGames.Count == 0;
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
            var statsTask = _api.GetOwnedGameStatsAsync(gameId);
            var versionsTask = _api.GetOwnedVersionsAsync(gameId);
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
    private void SelectTab(string? tag)
    {
        if (!string.IsNullOrWhiteSpace(tag))
        {
            SelectedTab = tag;
        }
    }

    [RelayCommand]
    private void AddGame() => _addGame?.Invoke();

    [RelayCommand]
    private void AddDlc(StudioContentItem? parent)
    {
        var target = parent ?? SelectedGame;
        if (target is null || string.IsNullOrWhiteSpace(target.Symbol) || target.IsDlc)
        {
            return;
        }

        _addDlc?.Invoke(target.Symbol);
    }

    [RelayCommand]
    private void AddVersion()
    {
        if (SelectedGame is not null && _addVersion is not null)
        {
            _addVersion(SelectedGame.Id);
        }
        else
        {
            _addGame?.Invoke();
        }
    }

    [RelayCommand]
    private async Task DownloadManifestAsync()
    {
        if (SelectedGame is null || _api is null || _saveFile is null)
        {
            return;
        }

        StatusText = await GameManifestSource.SaveAsync(_api, SelectedGame.Id, asAdmin: false, _saveFile)
                     ?? StatusText;
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
            var updated = await _api.UpdateGameAsync(item.Id, item.ToUpdateRequest());
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
    private async Task SetDraftAsync(bool published)
    {
        if (SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.UpdateGameAsync(SelectedGame.Id, new UpdateGameRequestDto { DraftP = !published });
        if (updated is null)
        {
            StatusText = Loc.T("Nie udało się zmienić widoczności.");
            return;
        }

        SelectedGame.ApplyDto(updated, _apiBaseUrl);
        StatusText = published ? Loc.T("Gra jest widoczna w sklepie.") : Loc.T("Gra jest szkicem — ukryta w sklepie.");
    }

    [RelayCommand]
    private Task PickCoverAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameCoverAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Zaktualizowano miniaturkę."));

    [RelayCommand]
    private Task ClearCoverAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameCoverAsync(id, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Usunięto miniaturkę."));

    [RelayCommand]
    private Task PickHeaderAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameHeaderAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Zaktualizowano okładkę poziomą."));

    [RelayCommand]
    private Task ClearHeaderAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameHeaderAsync(id, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Usunięto okładkę poziomą."));

    [RelayCommand]
    private Task PickLogoAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameLogoAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Zaktualizowano logo."));

    [RelayCommand]
    private Task ClearLogoAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameLogoAsync(id, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Usunięto logo."));

    [RelayCommand]
    private Task PickIntroAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickVideo,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameIntroAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Zaktualizowano intro."));

    [RelayCommand]
    private Task ClearIntroAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameIntroAsync(id, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Usunięto intro."));

    [RelayCommand]
    private Task AddScreenshotAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.AddGameScreenshotAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                Loc.T("Dodano zrzut ekranu."));

    [RelayCommand]
    private async Task RemoveScreenshotAsync(StudioScreenshotItem? shot)
    {
        if (shot is null || SelectedGame is null || _api is null || shot.Id <= 0)
        {
            return;
        }

        await ReloadAfterMediaAsync(
            await _api.RemoveGameScreenshotAsync(SelectedGame.Id, shot.Id, asAdmin: false),
            Loc.T("Usunięto zrzut ekranu."));
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

        var tab = SelectedTab;
        SelectedGame?.ApplyDto(updated, _apiBaseUrl);
        var listed = Games.FirstOrDefault(g => g.Id == updated.Id);
        if (listed is not null && !ReferenceEquals(listed, SelectedGame))
        {
            listed.ApplyDto(updated, _apiBaseUrl);
        }

        StatusText = okText;
        SelectedTab = tab;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveVersionNotesAsync(StudioVersionItem? version)
    {
        if (version is null || SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.UpdateVersionNotesAsync(SelectedGame.Id, version.Id, version.EditNotes);
        if (updated is null)
        {
            StatusText = Loc.T("Nie udało się zapisać opisu zmian.");
            return;
        }

        version.ApplyDto(updated);
        StatusText = Loc.F("Zapisano opis zmian dla {0}.", updated.Version);
    }

    [RelayCommand]
    private async Task DeleteAsync(StudioContentItem? item)
    {
        var target = item ?? SelectedGame;
        if (target is null || _api is null)
        {
            return;
        }

        if (_confirmDelete is not null && !await _confirmDelete(target))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var deleted = await _api.DeleteGameAsync(target.Id);
            StatusText = deleted
                ? Loc.F("Usunięto: {0}", target.Title)
                : Loc.T("Nie udało się usunąć (sprawdź uprawnienia lub powiązane DLC).");
            if (deleted)
            {
                if (SelectedGame?.Id == target.Id)
                {
                    SelectedGame = null;
                }

                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Błąd usuwania: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}

