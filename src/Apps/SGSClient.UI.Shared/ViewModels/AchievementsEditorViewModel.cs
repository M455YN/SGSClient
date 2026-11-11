using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Achievements;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>
/// The achievements tab shared by the Creator panel and the Admin panel, laid out like Steamworks'
/// Achievements and Stats pages: it edits the definitions of whichever game the host passes to
/// <see cref="LoadAsync"/>. The server decides who may edit what.
/// Achievements are edited one at a time in a dialog (<see cref="BeginEdit"/> → <see cref="CommitEditAsync"/>)
/// and every change is saved right away; the stats table is edited in place and saved with <see cref="SaveCommand"/>.
/// </summary>
public partial class AchievementsEditorViewModel(
    ISgsApiService? api,
    string apiBaseUrl,
    Action<string> setStatus,
    Action<bool> setBusy) : ObservableObject
{
    private Func<Task<string?>>? _pickImage;
    private Func<string, Task<string?>>? _makeGreyscale;
    private int _gameId;

    /// <summary>The table row the dialog edits (null for a new achievement) and its working copy.</summary>
    private StudioAchievementItem? _editing;
    private StudioAchievementItem? _draft;

    /// <summary>Raised after a save with whether the game now has any achievements.</summary>
    public event Action<bool>? Saved;

    public ObservableCollection<StudioAchievementItem> Items { get; } = [];
    public ObservableCollection<StudioStatItem> Stats { get; } = [];

    [ObservableProperty] public partial bool HasItems { get; set; }
    [ObservableProperty] public partial bool HasStats { get; set; }
    [ObservableProperty] public partial string Summary { get; set; } = string.Empty;

    public string Help { get; } = Loc.T(
        "Gra uruchomiona z SGS dostaje zmienne środowiskowe SGS_API_URL, SGS_GAME_ID i SGS_ACHIEVEMENTS_TOKEN. "
        + "Odblokowanie to POST na <SGS_API_URL>/api/sdk/achievements/<API name>/unlock z nagłówkiem X-SGS-Token; "
        + "licznik: POST .../progress z JSON-em {\"value\": 5}; statystyka: POST <SGS_API_URL>/api/sdk/stats/<API name> "
        + "z {\"value\": 5} — osiągnięcia powiązane ze statystyką odblokują się same po osiągnięciu celu. "
        + "Bez sieci wywołania trafiają do kolejki w kliencie. Przykłady dla Unity, Godota i Unreala: docs/creators/achievements.md.");

    public bool IsReady => _gameId != 0 && api is not null;

    public void SetPickImageHandler(Func<Task<string?>> pick) => _pickImage = pick;

    /// <summary>Platform code that turns an icon URL into a greyscale PNG on disk (null when it cannot).</summary>
    public void SetGreyscaleHandler(Func<string, Task<string?>> makeGreyscale) => _makeGreyscale = makeGreyscale;

    public void Clear()
    {
        _gameId = 0;
        Items.Clear();
        Stats.Clear();
        Refresh();
    }

    public async Task LoadAsync(int gameId)
    {
        if (api is null)
        {
            return;
        }

        _gameId = gameId;
        try
        {
            var definitions = await api.GetAchievementDefinitionsAsync(gameId);
            var stats = await api.GetStatDefinitionsAsync(gameId);
            if (_gameId == gameId)
            {
                ApplyStats(stats);
                Apply(definitions);
            }
        }
        catch (Exception ex)
        {
            setStatus(Loc.F("Błąd ładowania: {0}", ex.Message));
        }
    }

    private void Apply(IReadOnlyList<AchievementDefinitionDto> definitions)
    {
        Items.Clear();
        foreach (var definition in definitions)
        {
            Items.Add(StudioAchievementItem.FromDto(definition, apiBaseUrl));
        }

        Refresh();
    }

    private void ApplyStats(IReadOnlyList<GameStatDefinitionDto> stats)
    {
        Stats.Clear();
        foreach (var stat in stats)
        {
            Stats.Add(StudioStatItem.FromDto(stat));
        }
    }

    private void Refresh()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].RowId = (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        for (var i = 0; i < Stats.Count; i++)
        {
            Stats[i].RowId = (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        HasItems = Items.Count > 0;
        HasStats = Stats.Count > 0;
        var points = Items.Sum(a => int.TryParse(a.EditPoints, out var p) ? p : 0);
        Summary = HasItems
            ? Loc.F("{0} osiągnięć · {1} pkt", Items.Count, points)
            : Loc.T("Ta gra nie ma jeszcze osiągnięć.");
    }

    private List<string> StatKeys() =>
        Stats.Select(s => AchievementRules.NormalizeKey(s.EditKey)).OfType<string>().Distinct().ToList();

    // ---- achievement dialog ----------------------------------------------------------------

    /// <summary>
    /// Starts editing <paramref name="original"/> (or a new achievement when null) and returns the working
    /// copy the dialog binds to; null when no game is loaded.
    /// </summary>
    public StudioAchievementItem? BeginEdit(StudioAchievementItem? original)
    {
        if (!IsReady)
        {
            return null;
        }

        EndEdit();
        _editing = original;
        _draft = original?.Clone() ?? StudioAchievementItem.CreateNew(apiBaseUrl);
        _draft.AvailableStats = StatKeys();
        if (original is null)
        {
            _draft.RowId = (Items.Count + 1).ToString(CultureInfo.InvariantCulture);
        }

        _draft.PropertyChanged += OnDraftChanged;
        ValidateDraft();
        return _draft;
    }

    /// <summary>Closes the dialog's working copy; unsaved changes are dropped.</summary>
    public void EndEdit()
    {
        if (_draft is not null)
        {
            _draft.PropertyChanged -= OnDraftChanged;
        }

        _draft = null;
        _editing = null;
    }

    /// <summary>Saves the dialog's achievement to the server. False keeps the dialog open with the reason shown.</summary>
    public async Task<bool> CommitEditAsync()
    {
        var draft = _draft;
        if (draft is null)
        {
            return false;
        }

        ValidateDraft();
        if (draft.HasValidationError)
        {
            return false;
        }

        var rows = Items.ToList();
        var index = _editing is null ? -1 : rows.IndexOf(_editing);
        if (index >= 0)
        {
            rows[index] = draft;
        }
        else
        {
            rows.Add(draft);
        }

        var error = await SyncAsync(rows);
        if (error is not null)
        {
            draft.ValidationError = error;
            return false;
        }

        // From now on the dialog edits the saved row (an icon upload may keep it open).
        var key = AchievementRules.NormalizeKey(draft.EditKey);
        _editing = Items.FirstOrDefault(i => i.EditKey == key);
        draft.IsNew = false;
        draft.EditKey = key ?? draft.EditKey;
        return true;
    }

    private void OnDraftChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is { } name && name.StartsWith("Edit", StringComparison.Ordinal))
        {
            ValidateDraft();
        }
    }

    /// <summary>Puts the first problem of the dialog's fields into <see cref="StudioAchievementItem.ValidationError"/>.</summary>
    private void ValidateDraft()
    {
        if (_draft is not { } d)
        {
            return;
        }

        string? error;
        if (string.IsNullOrWhiteSpace(d.EditTitle))
        {
            error = Loc.T("Podaj nazwę osiągnięcia.");
        }
        else if (d.ShowsTarget && string.IsNullOrWhiteSpace(d.EditTarget))
        {
            error = Loc.F("Podaj cel (co najmniej {0}).", AchievementRules.MinTarget);
        }
        else if (d.IsStatKind && string.IsNullOrWhiteSpace(d.EditStatKey))
        {
            error = d.AvailableStats.Count == 0
                ? Loc.T("Najpierw dodaj statystykę w tabeli „Statystyki”.")
                : Loc.T("Wybierz statystykę.");
        }
        else if (!d.TryToDto(0, out var dto, out var parseError))
        {
            error = parseError;
        }
        else if (AchievementRules.NormalizeKey(dto!.Key) is { } key && Items.Any(i => i != _editing && i.EditKey == key))
        {
            error = Loc.F("API name „{0}” jest już zajęty.", key);
        }
        else
        {
            error = AchievementRules.Validate([dto], StatKeys());
        }

        d.ValidationError = error ?? string.Empty;
    }

    /// <summary>Picks a file and uploads it as the dialog's achieved or unachieved icon.</summary>
    public async Task PickDraftIconAsync(bool locked)
    {
        if (_draft is null || _pickImage is null)
        {
            return;
        }

        var path = await _pickImage();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await UploadDraftIconAsync(path, locked);
        }
    }

    /// <summary>Uploads a greyscale copy of the achieved icon as the unachieved one.</summary>
    public async Task MakeGreyscaleLockedIconAsync()
    {
        if (_draft?.IconPreviewUrl is not { } source)
        {
            setStatus(Loc.T("Najpierw dodaj ikonę zdobytą."));
            return;
        }

        if (_makeGreyscale is null)
        {
            return;
        }

        setBusy(true);
        string? path = null;
        try
        {
            path = await _makeGreyscale(source);
        }
        catch
        {
            path = null;
        }
        finally
        {
            setBusy(false);
        }

        if (path is null)
        {
            setStatus(Loc.T("Nie udało się utworzyć szarej wersji ikony."));
            return;
        }

        try
        {
            await UploadDraftIconAsync(path, locked: true);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // a temp file left behind is harmless
            }
        }
    }

    public void ClearDraftIcon(bool locked)
    {
        if (_draft is null)
        {
            return;
        }

        if (locked)
        {
            _draft.EditLockedIconUrl = string.Empty;
        }
        else
        {
            _draft.EditIconUrl = string.Empty;
        }
    }

    private async Task UploadDraftIconAsync(string path, bool locked)
    {
        var draft = _draft;
        if (draft is null || api is null)
        {
            return;
        }

        // The file is named after the API name, so a new achievement is saved first (like the game editor).
        if (draft.IsNew && !await CommitEditAsync())
        {
            return;
        }

        setBusy(true);
        try
        {
            var (updated, error) = await api.UploadAchievementIconAsync(_gameId, draft.EditKey, path, locked);
            if (updated is null)
            {
                setStatus(Loc.F("Nie udało się wgrać ikony: {0}", error ?? string.Empty));
                return;
            }

            // The server already points the achievement at the file, so the table row follows too.
            foreach (var target in new[] { draft, _editing }.OfType<StudioAchievementItem>())
            {
                if (locked)
                {
                    target.EditLockedIconUrl = updated.LockedIconUrl ?? string.Empty;
                }
                else
                {
                    target.EditIconUrl = updated.IconUrl ?? string.Empty;
                }
            }

            setStatus(Loc.T("Zaktualizowano ikonę osiągnięcia."));
        }
        finally
        {
            setBusy(false);
        }
    }

    /// <summary>Deletes (deactivates) an achievement and saves at once; players keep what they unlocked.</summary>
    public async Task DeleteAsync(StudioAchievementItem item)
    {
        if (!IsReady || !Items.Contains(item))
        {
            return;
        }

        if (await SyncAsync(Items.Where(i => i != item).ToList()) is { } error)
        {
            setStatus(error);
        }
    }

    [RelayCommand]
    private Task MoveUp(StudioAchievementItem? item) => MoveAsync(item, -1);

    [RelayCommand]
    private Task MoveDown(StudioAchievementItem? item) => MoveAsync(item, 1);

    private async Task MoveAsync(StudioAchievementItem? item, int offset)
    {
        var rows = Items.ToList();
        var index = item is null ? -1 : rows.IndexOf(item);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= rows.Count)
        {
            return;
        }

        (rows[index], rows[target]) = (rows[target], rows[index]);
        if (await SyncAsync(rows) is { } error)
        {
            setStatus(error);
        }
    }

    // ---- stats table -----------------------------------------------------------------------

    [RelayCommand]
    private void AddStat()
    {
        if (_gameId == 0)
        {
            return;
        }

        Stats.Add(new StudioStatItem { IsNew = true, IsEditing = true });
        Refresh();
    }

    [RelayCommand]
    private void RemoveStat(StudioStatItem? item)
    {
        if (item is null || !Stats.Remove(item))
        {
            return;
        }

        Refresh();
        setStatus(Loc.T("Usunięto z listy. Zapisz statystyki, aby zatwierdzić zmianę."));
    }

    [RelayCommand]
    private void ToggleEdit(StudioStatItem? item)
    {
        if (item is not null)
        {
            item.IsEditing = !item.IsEditing;
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (_gameId != 0)
        {
            await LoadAsync(_gameId);
            setStatus(Loc.T("Wczytano osiągnięcia z serwera."));
        }
    }

    /// <summary>Saves the stats table (and the achievements as they are).</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (await SyncAsync(Items.ToList()) is { } error)
        {
            setStatus(error);
        }
    }

    /// <summary>Sends <paramref name="rows"/> (in order) plus the stats table; null on success, else the reason.</summary>
    private async Task<string?> SyncAsync(IReadOnlyList<StudioAchievementItem> rows)
    {
        if (!IsReady)
        {
            return Loc.T("Najpierw wybierz grę.");
        }

        var definitions = new List<AchievementDefinitionDto>();
        foreach (var item in rows)
        {
            if (!item.TryToDto(definitions.Count, out var dto, out var parseError))
            {
                return parseError;
            }

            definitions.Add(dto!);
        }

        var stats = new List<GameStatDefinitionDto>();
        foreach (var item in Stats)
        {
            if (!item.TryToDto(stats.Count, out var dto, out var parseError))
            {
                return parseError;
            }

            stats.Add(dto!);
        }

        var statKeys = stats.Select(s => AchievementRules.NormalizeKey(s.Key) ?? s.Key).ToList();
        if ((AchievementRules.ValidateStats(stats) ?? AchievementRules.Validate(definitions, statKeys)) is { } rulesError)
        {
            return rulesError;
        }

        setBusy(true);
        try
        {
            var gameId = _gameId;
            var (saved, error) = await api!.SyncAchievementDefinitionsAsync(gameId, definitions, stats);
            if (saved is null)
            {
                return Loc.F("Nie udało się zapisać osiągnięć: {0}", error ?? string.Empty);
            }

            var savedStats = await api.GetStatDefinitionsAsync(gameId);
            if (_gameId == gameId)
            {
                ApplyStats(savedStats);
                Apply(saved);
                // The server turns the store's "Achievements" feature on or off to match the list.
                Saved?.Invoke(saved.Count > 0);
            }

            setStatus(Loc.F("Zapisano osiągnięcia: {0}.", saved.Count));
            return null;
        }
        catch (Exception ex)
        {
            return Loc.F("Błąd zapisu: {0}", ex.Message);
        }
        finally
        {
            setBusy(false);
        }
    }

    // ---- testing ---------------------------------------------------------------------------

    [RelayCommand]
    private async Task TestUnlockAsync(StudioAchievementItem? item)
    {
        if (item is null || _gameId == 0 || api is null)
        {
            return;
        }

        if (item.IsNew)
        {
            setStatus(Loc.T("Najpierw zapisz osiągnięcia, potem je przetestuj."));
            return;
        }

        var (result, error) = await api.TestUnlockAchievementAsync(_gameId, item.EditKey);
        if (result is null)
        {
            setStatus(Loc.F("Nie udało się odblokować: {0}", error ?? string.Empty));
            return;
        }

        if (result.JustUnlocked)
        {
            item.UnlockedCount++;
            if (item == _draft && _editing is not null)
            {
                _editing.UnlockedCount = item.UnlockedCount;
            }

            setStatus(Loc.F("Odblokowano „{0}” na Twoim koncie.", item.EditTitle));
        }
        else
        {
            setStatus(Loc.F("„{0}” było już odblokowane.", item.EditTitle));
        }
    }

    [RelayCommand]
    private async Task ResetProgressAsync()
    {
        if (_gameId == 0 || api is null)
        {
            return;
        }

        var ok = await api.ResetMyAchievementProgressAsync(_gameId);
        setStatus(ok
            ? Loc.T("Wyzerowano Twoje osiągnięcia i statystyki w tej grze.")
            : Loc.T("Nie udało się wyzerować postępu."));
    }
}
