using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Packaging;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class StudioViewModel : ObservableObject
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly ISgsApiService? _api;
    private readonly IAuthSession? _auth;
    private readonly string _apiBaseUrl;
    private Action? _addGame;
    private Action<string>? _addDlc;
    private Func<StudioContentItem, Task<bool>>? _confirmDelete;
    private Func<Task<string?>>? _pickImage;
    private Func<Task<string?>>? _pickVideo;
    private int _selectedId;

    public StudioViewModel(ISgsApiService? api, IAuthSession? auth, IConfiguration? configuration = null)
    {
        _api = api;
        _auth = auth;
        _apiBaseUrl = configuration?.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
            ?? new ApiClientSettings().BaseUrl;
        StatusText = "Ładowanie Twoich gier…";
    }

    public string Title { get; } = "Moje gry";
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

        var updated = await _api.UploadGameCoverAsync(SelectedGame.Id, path, asAdmin: false);
        await ReloadAfterMediaAsync(updated, "Zaktualizowano okładkę.");
    }

    [RelayCommand]
    private async Task ClearCoverAsync()
    {
        if (SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.ClearGameCoverAsync(SelectedGame.Id, asAdmin: false);
        await ReloadAfterMediaAsync(updated, "Usunięto okładkę.");
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

        var updated = await _api.UploadGameIntroAsync(SelectedGame.Id, path, asAdmin: false);
        await ReloadAfterMediaAsync(updated, "Zaktualizowano intro.");
    }

    [RelayCommand]
    private async Task ClearIntroAsync()
    {
        if (SelectedGame is null || _api is null)
        {
            return;
        }

        var updated = await _api.ClearGameIntroAsync(SelectedGame.Id, asAdmin: false);
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

        var updated = await _api.AddGameScreenshotAsync(SelectedGame.Id, path, asAdmin: false);
        await ReloadAfterMediaAsync(updated, "Dodano zrzut ekranu.");
    }

    [RelayCommand]
    private async Task RemoveScreenshotAsync(StudioScreenshotItem? shot)
    {
        if (shot is null || SelectedGame is null || _api is null || shot.Id <= 0)
        {
            return;
        }

        var updated = await _api.RemoveGameScreenshotAsync(SelectedGame.Id, shot.Id, asAdmin: false);
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

public partial class StudioContentItem : ObservableObject
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required string Symbol { get; init; }
    public bool IsDlc { get; init; }
    public string? ParentSymbol { get; init; }
    public ObservableCollection<StudioContentItem> Dlcs { get; } = [];
    public ObservableCollection<StudioScreenshotItem> ScreenshotItems { get; } = [];
    public ObservableCollection<StudioBuildItem> Builds { get; } = [];

    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial string? HardwareRequirements { get; set; }
    [ObservableProperty] public partial string? OtherInformation { get; set; }
    [ObservableProperty] public partial string? GenreSymbol { get; set; }
    [ObservableProperty] public partial string? EngineSymbol { get; set; }
    [ObservableProperty] public partial string? CoverUrl { get; set; }
    [ObservableProperty] public partial string? IntroUrl { get; set; }
    [ObservableProperty] public partial string? CoverImageUrl { get; set; }
    [ObservableProperty] public partial bool DraftP { get; set; }
    [ObservableProperty] public partial bool FeaturedP { get; set; }
    [ObservableProperty] public partial string? StudioName { get; set; }
    [ObservableProperty] public partial string? WebsiteUrl { get; set; }
    [ObservableProperty] public partial string? DiscordUrl { get; set; }
    [ObservableProperty] public partial string? SupportUrl { get; set; }
    [ObservableProperty] public partial GameReleaseStatus ReleaseStatus { get; set; }
    [ObservableProperty] public partial DateTimeOffset? ReleaseDateUtc { get; set; }
    [ObservableProperty] public partial GameAgeRating AgeRating { get; set; }
    [ObservableProperty] public partial GameFeatures Features { get; set; }
    [ObservableProperty] public partial string LanguagesText { get; set; } = string.Empty;
    [ObservableProperty] public partial string TagsText { get; set; } = string.Empty;
    [ObservableProperty] public partial string PackageSizeText { get; set; } = "—";
    [ObservableProperty] public partial string CurrentVersionText { get; set; } = "—";
    [ObservableProperty] public partial string CreatedText { get; set; } = "—";

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

    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImageUrl);
    public bool HasIntro => !string.IsNullOrWhiteSpace(IntroUrl);
    public bool HasDlcs => Dlcs.Count > 0;
    public bool IsPublished => !DraftP;
    public string KindLabel => IsDlc ? "DLC" : "Gra";
    public string DraftLabel => DraftP ? "Szkic" : "W sklepie";
    public string ReleaseLabel => GameStoreLabels.ReleaseStatus(ReleaseStatus);
    public string AgeLabel => GameStoreLabels.AgeRating(AgeRating);
    public string MetaText => IsDlc
        ? $"{Symbol} · DLC · {DraftLabel}"
        : $"{Symbol} · {DraftLabel} · {Dlcs.Count} DLC · {ReleaseLabel}";
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

    public static StudioContentItem FromGame(GameDto game, string apiBaseUrl)
    {
        var item = new StudioContentItem
        {
            Id = game.Id,
            Title = game.Title,
            Symbol = game.Symbol,
            IsDlc = game.Kind == GameContentKind.Dlc,
            ParentSymbol = game.ParentSymbol
        };
        item.ApplyDto(game, apiBaseUrl);
        return item;
    }

    public static StudioContentItem FromDlc(GameDlcDto dlc, string parentSymbol, bool parentDraft, string apiBaseUrl) => new()
    {
        Id = dlc.Id,
        Title = dlc.Title,
        Symbol = dlc.Symbol,
        IsDlc = true,
        ParentSymbol = parentSymbol,
        CoverUrl = dlc.CoverUrl,
        CoverImageUrl = string.IsNullOrWhiteSpace(dlc.CoverUrl)
            ? null
            : InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, dlc.CoverUrl),
        DraftP = parentDraft,
        EditTitle = dlc.Title,
        PackageSizeText = FormatSize(dlc.PackageSizeBytes),
        CurrentVersionText = dlc.PreferredVersion ?? "—"
    };

    public void ApplyDto(GameDto game, string apiBaseUrl)
    {
        Description = game.Description;
        HardwareRequirements = game.HardwareRequirements;
        OtherInformation = game.OtherInformation;
        GenreSymbol = game.GenreSymbol;
        EngineSymbol = game.EngineSymbol;
        CoverUrl = game.CoverUrl;
        IntroUrl = game.IntroUrl;
        CoverImageUrl = string.IsNullOrWhiteSpace(game.CoverUrl)
            ? null
            : InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, game.CoverUrl);
        DraftP = game.DraftP;
        FeaturedP = game.FeaturedP;
        StudioName = game.StudioName;
        WebsiteUrl = game.WebsiteUrl;
        DiscordUrl = game.DiscordUrl;
        SupportUrl = game.SupportUrl;
        ReleaseStatus = game.ReleaseStatus;
        ReleaseDateUtc = game.ReleaseDateUtc;
        AgeRating = game.AgeRating;
        Features = game.Features;
        LanguagesText = string.Join(", ", game.Languages);
        TagsText = string.Join(", ", game.Tags);
        PackageSizeText = FormatSize(game.PackageSizeBytes);
        CurrentVersionText = game.Builds.FirstOrDefault()?.CurrentVersion ?? "—";
        CreatedText = game.CreatedAtUtc.LocalDateTime.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("pl-PL"));

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

        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(HasIntro));
        OnPropertyChanged(nameof(IsPublished));
        OnPropertyChanged(nameof(DraftLabel));
        OnPropertyChanged(nameof(ReleaseLabel));
        OnPropertyChanged(nameof(AgeLabel));
        OnPropertyChanged(nameof(MetaText));
        OnPropertyChanged(nameof(HasDlcs));
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "—";
        }

        var mb = bytes / (1024d * 1024d);
        return mb >= 1024 ? $"{mb / 1024d:0.0} GB" : $"{mb:0.0} MB";
    }
}

public sealed class StudioScreenshotItem
{
    public required int Id { get; init; }
    public required string ImageUrl { get; init; }
}

public sealed class StudioBuildItem
{
    public required string PlatformText { get; init; }
    public required string Version { get; init; }
    public required string ExeName { get; init; }
    public bool IsDefault { get; init; }
    public string MetaText => IsDefault ? $"{ExeName} · domyślny" : ExeName;
}

public partial class StudioVersionItem : ObservableObject
{
    public required int Id { get; init; }
    public required string Version { get; init; }
    public string? PlatformLabel { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }
    public bool IsCurrent { get; init; }

    [ObservableProperty] public partial string EditNotes { get; set; } = string.Empty;

    public string MetaText =>
        (string.IsNullOrWhiteSpace(PlatformLabel) ? "build" : PlatformLabel)
        + " · "
        + PublishedAtUtc.LocalDateTime.ToString("d MMM yyyy HH:mm", CultureInfo.GetCultureInfo("pl-PL"))
        + (IsCurrent ? " · aktualna" : "");

    public static StudioVersionItem FromDto(GameBuildVersionDto dto)
    {
        var item = new StudioVersionItem
        {
            Id = dto.Id,
            Version = dto.Version,
            PlatformLabel = dto.PlatformLabel,
            PublishedAtUtc = dto.PublishedAtUtc,
            IsCurrent = dto.IsCurrent
        };
        item.ApplyDto(dto);
        return item;
    }

    public void ApplyDto(GameBuildVersionDto dto) => EditNotes = dto.ReleaseNotes ?? string.Empty;
}

public sealed class StudioStatBar
{
    public required string Label { get; init; }
    public required int Value { get; init; }
    public required int Max { get; init; }
    public double BarHeight => 8 + (Max <= 0 ? 0 : 48.0 * Value / Max);
    public string Tooltip => $"{Label}: {Value}";
}
