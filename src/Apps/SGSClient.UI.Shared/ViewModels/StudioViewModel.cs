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
    private Action<string>? _addDlc;
    private Func<StudioContentItem, Task<bool>>? _confirmDelete;
    private Func<Task<string?>>? _pickImage;
    private Func<Task<string?>>? _pickVideo;
    private int _selectedId;

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
        StatusText = "Ładowanie Twoich gier…";
    }

    public string Title { get; } = "Panel twórcy";
    public IReadOnlyList<string> GenreOptions { get; } =
        SgsGameTaxonomy.Genres.Select(g => g.Symbol).Prepend("").ToArray();
    public IReadOnlyList<string> EngineOptions { get; } =
        SgsGameTaxonomy.Engines.Select(e => e.Symbol).Prepend("").ToArray();
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

    public bool HasSelection => SelectedGame is not null;
    public bool IsOverviewTab => SelectedTab == "overview";
    public bool IsStoreTab => SelectedTab == "store";
    public bool IsMediaTab => SelectedTab == "media";
    public bool IsBuildsTab => SelectedTab == "builds";
    public bool IsDlcTab => SelectedTab == "dlc";

    public void SetAddGameHandler(Action add) => _addGame = add;
    public void SetAddDlcHandler(Action<string> add) => _addDlc = add;
    public void SetConfirmDeleteHandler(Func<StudioContentItem, Task<bool>> confirm) => _confirmDelete = confirm;
    public void SetPickImageHandler(Func<Task<string?>> pick) => _pickImage = pick;
    public void SetPickVideoHandler(Func<Task<string?>> pick) => _pickVideo = pick;

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsOverviewTab));
        OnPropertyChanged(nameof(IsStoreTab));
        OnPropertyChanged(nameof(IsMediaTab));
        OnPropertyChanged(nameof(IsBuildsTab));
        OnPropertyChanged(nameof(IsDlcTab));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedGameChanged(StudioContentItem? value)
    {
        _selectedId = value?.Id ?? 0;
        OnPropertyChanged(nameof(HasSelection));
        _ = LoadSelectionExtrasAsync();
    }

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
            FilteredGames.Clear();
            SelectedGame = null;
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
    private void AddVersion() => _addGame?.Invoke();

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
                StatusText = "Nie udało się zapisać strony sklepu.";
                return;
            }

            item.ApplyDto(updated, _apiBaseUrl);
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
    private async Task SetDraftAsync(bool published)
    {
        if (SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.UpdateGameAsync(SelectedGame.Id, new UpdateGameRequestDto { DraftP = !published });
        if (updated is null)
        {
            StatusText = "Nie udało się zmienić widoczności.";
            return;
        }

        SelectedGame.ApplyDto(updated, _apiBaseUrl);
        StatusText = published ? "Gra jest widoczna w sklepie." : "Gra jest szkicem — ukryta w sklepie.";
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
                "Zaktualizowano miniaturkę.");

    [RelayCommand]
    private Task ClearCoverAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameCoverAsync(id, asAdmin: false),
                ReloadAfterMediaAsync,
                "Usunięto miniaturkę.");

    [RelayCommand]
    private Task PickHeaderAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameHeaderAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                "Zaktualizowano okładkę poziomą.");

    [RelayCommand]
    private Task ClearHeaderAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameHeaderAsync(id, asAdmin: false),
                ReloadAfterMediaAsync,
                "Usunięto okładkę poziomą.");

    [RelayCommand]
    private Task PickLogoAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameLogoAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                "Zaktualizowano logo.");

    [RelayCommand]
    private Task ClearLogoAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameLogoAsync(id, asAdmin: false),
                ReloadAfterMediaAsync,
                "Usunięto logo.");

    [RelayCommand]
    private Task PickIntroAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickVideo,
                SelectedGame?.Id,
                (id, path) => _api.UploadGameIntroAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                "Zaktualizowano intro.");

    [RelayCommand]
    private Task ClearIntroAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.ClearAsync(
                SelectedGame?.Id,
                id => _api.ClearGameIntroAsync(id, asAdmin: false),
                ReloadAfterMediaAsync,
                "Usunięto intro.");

    [RelayCommand]
    private Task AddScreenshotAsync() =>
        _api is null
            ? Task.CompletedTask
            : GameMediaActions.PickAsync(
                _pickImage,
                SelectedGame?.Id,
                (id, path) => _api.AddGameScreenshotAsync(id, path, asAdmin: false),
                ReloadAfterMediaAsync,
                "Dodano zrzut ekranu.");

    [RelayCommand]
    private async Task RemoveScreenshotAsync(StudioScreenshotItem? shot)
    {
        if (shot is null || SelectedGame is null || _api is null || shot.Id <= 0)
        {
            return;
        }

        await ReloadAfterMediaAsync(
            await _api.RemoveGameScreenshotAsync(SelectedGame.Id, shot.Id, asAdmin: false),
            "Usunięto zrzut ekranu.");
    }

    private Task ReloadAfterMediaAsync(GameDto? updated, string okText)
    {
        if (updated is null)
        {
            StatusText = okText.StartsWith("Błąd", StringComparison.OrdinalIgnoreCase)
                ? okText
                : "Nie udało się zaktualizować mediów (serwer odrzucił plik lub brak uprawnień).";
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
            StatusText = "Nie udało się zapisać opisu zmian.";
            return;
        }

        version.ApplyDto(updated);
        StatusText = $"Zapisano opis zmian dla {updated.Version}.";
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
                ? $"Usunięto: {target.Title}"
                : "Nie udało się usunąć (sprawdź uprawnienia lub powiązane DLC).";
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
            StatusText = $"Błąd usuwania: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

