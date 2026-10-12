using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Achievements;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>
/// The achievements tab shared by the Creator panel and the Admin panel: it edits the definitions
/// of whichever game the host passes to <see cref="LoadAsync"/>. The server decides who may edit what.
/// </summary>
public partial class AchievementsEditorViewModel(
    ISgsApiService? api,
    string apiBaseUrl,
    Action<string> setStatus,
    Action<bool> setBusy) : ObservableObject
{
    private Func<Task<string?>>? _pickImage;
    private int _gameId;

    /// <summary>Raised after a save with whether the game now has any achievements.</summary>
    public event Action<bool>? Saved;

    public ObservableCollection<StudioAchievementItem> Items { get; } = [];

    [ObservableProperty] public partial bool HasItems { get; set; }
    [ObservableProperty] public partial string Summary { get; set; } = string.Empty;

    public string Help { get; } = Loc.T(
        "Gra uruchomiona z SGS dostaje zmienne środowiskowe SGS_API_URL, SGS_GAME_ID i SGS_ACHIEVEMENTS_TOKEN. "
        + "Odblokowanie to POST na <SGS_API_URL>/api/sdk/achievements/<klucz>/unlock z nagłówkiem X-SGS-Token; "
        + "licznik: POST .../progress z JSON-em {\"value\": 5}. Bez sieci wywołania trafiają do kolejki w kliencie. "
        + "Przykłady dla Unity, Godota i Unreala: docs/creators/achievements.md.");

    public void SetPickImageHandler(Func<Task<string?>> pick) => _pickImage = pick;

    public void Clear()
    {
        _gameId = 0;
        Items.Clear();
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
            if (_gameId == gameId)
            {
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

    private void Refresh()
    {
        HasItems = Items.Count > 0;
        var points = Items.Sum(a => int.TryParse(a.EditPoints, out var p) ? p : 0);
        Summary = HasItems
            ? Loc.F("{0} osiągnięć · {1} pkt", Items.Count, points)
            : Loc.T("Ta gra nie ma jeszcze osiągnięć.");
    }

    [RelayCommand]
    private void Add()
    {
        if (_gameId == 0)
        {
            return;
        }

        Items.Add(new StudioAchievementItem { IsNew = true, ApiBaseUrl = apiBaseUrl });
        Refresh();
    }

    [RelayCommand]
    private void Remove(StudioAchievementItem? item)
    {
        if (item is null || !Items.Remove(item))
        {
            return;
        }

        Refresh();
        setStatus(Loc.T("Usunięto z listy. Zapisz osiągnięcia, aby zatwierdzić zmianę."));
    }

    [RelayCommand]
    private void MoveUp(StudioAchievementItem? item) => Move(item, -1);

    [RelayCommand]
    private void MoveDown(StudioAchievementItem? item) => Move(item, 1);

    private void Move(StudioAchievementItem? item, int offset)
    {
        var index = item is null ? -1 : Items.IndexOf(item);
        var target = index + offset;
        if (index >= 0 && target >= 0 && target < Items.Count)
        {
            Items.Move(index, target);
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_gameId == 0 || api is null)
        {
            return;
        }

        var definitions = new List<AchievementDefinitionDto>();
        foreach (var item in Items)
        {
            if (!item.TryToDto(definitions.Count, out var dto, out var parseError))
            {
                setStatus(parseError!);
                return;
            }

            definitions.Add(dto!);
        }

        if (AchievementRules.Validate(definitions) is { } rulesError)
        {
            setStatus(rulesError);
            return;
        }

        setBusy(true);
        try
        {
            var gameId = _gameId;
            var (saved, error) = await api.SyncAchievementDefinitionsAsync(gameId, definitions);
            if (saved is null)
            {
                setStatus(Loc.F("Nie udało się zapisać osiągnięć: {0}", error ?? string.Empty));
                return;
            }

            if (_gameId == gameId)
            {
                Apply(saved);
                // The server turns the store's "Achievements" feature on or off to match the list.
                Saved?.Invoke(saved.Count > 0);
            }

            setStatus(Loc.F("Zapisano osiągnięcia: {0}.", saved.Count));
        }
        catch (Exception ex)
        {
            setStatus(Loc.F("Błąd zapisu: {0}", ex.Message));
        }
        finally
        {
            setBusy(false);
        }
    }

    [RelayCommand]
    private async Task PickIconAsync(StudioAchievementItem? item)
    {
        if (item is null || _gameId == 0 || api is null || _pickImage is null)
        {
            return;
        }

        if (item.IsNew)
        {
            setStatus(Loc.T("Najpierw zapisz osiągnięcia, potem dodaj ikonę."));
            return;
        }

        var path = await _pickImage();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        setBusy(true);
        try
        {
            var (updated, error) = await api.UploadAchievementIconAsync(_gameId, item.EditKey, path);
            if (updated is null)
            {
                setStatus(Loc.F("Nie udało się wgrać ikony: {0}", error ?? string.Empty));
                return;
            }

            item.EditIconUrl = updated.IconUrl ?? string.Empty;
            setStatus(Loc.T("Zaktualizowano ikonę osiągnięcia."));
        }
        finally
        {
            setBusy(false);
        }
    }

    [RelayCommand]
    private void ClearIcon(StudioAchievementItem? item)
    {
        if (item is not null)
        {
            item.EditIconUrl = string.Empty;
            setStatus(Loc.T("Usunięto ikonę z listy. Zapisz osiągnięcia, aby zatwierdzić zmianę."));
        }
    }

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
            ? Loc.T("Wyzerowano Twój postęp w osiągnięciach tej gry.")
            : Loc.T("Nie udało się wyzerować postępu."));
    }
}
