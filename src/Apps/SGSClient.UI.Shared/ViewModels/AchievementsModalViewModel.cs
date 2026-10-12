using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>Steam-style achievements window for one game: my progress, global rarity, search.</summary>
public partial class AchievementsModalViewModel(ISgsApiService? api, string apiBaseUrl, int gameId, string title)
    : ObservableObject
{
    private List<PlayerAchievementItem> _all = [];

    public string Title { get; } = title;
    public ObservableCollection<PlayerAchievementItem> Visible { get; } = [];

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProgressText { get; set; } = string.Empty;
    [ObservableProperty] public partial string PercentText { get; set; } = string.Empty;
    [ObservableProperty] public partial string EmptyText { get; set; } = string.Empty;
    [ObservableProperty] public partial double Fraction { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGlobalTab))]
    public partial bool IsMineTab { get; set; } = true;

    public bool IsGlobalTab => !IsMineTab;

    public async Task LoadAsync()
    {
        var result = api is null
            ? null
            : await api.GetGameAchievementsAsync(gameId, Loc.IsEnglish ? "en" : null);
        _all = result?.Items.Select(i => PlayerAchievementItem.FromDto(i, apiBaseUrl)).ToList() ?? [];

        var total = result?.Total ?? 0;
        var unlocked = result?.Unlocked ?? 0;
        Fraction = total == 0 ? 0 : (double)unlocked / total;
        ProgressText = Loc.F("ZDOBYTO {0} Z {1} OSIĄGNIĘĆ", unlocked, total);
        PercentText = $"({Math.Round(Fraction * 100)}%)";
        EmptyText = Loc.T("Ta gra nie ma jeszcze osiągnięć.");
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    partial void OnIsMineTabChanged(bool value) => Refresh();

    [RelayCommand]
    private void SelectTab(string? tab) => IsMineTab = tab != "global";

    private void Refresh()
    {
        var query = SearchText.Trim();
        IEnumerable<PlayerAchievementItem> items = _all.Where(i =>
            query.Length == 0
            || i.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || i.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        items = IsMineTab
            ? items.OrderByDescending(i => i.UnlockedAtUtc is not null).ThenByDescending(i => i.UnlockedAtUtc)
            : items.OrderByDescending(i => i.RarityPercent);

        Visible.Clear();
        foreach (var item in items)
        {
            Visible.Add(item);
        }
    }
}
