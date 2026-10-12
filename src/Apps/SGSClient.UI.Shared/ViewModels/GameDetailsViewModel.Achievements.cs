using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

public partial class GameDetailsViewModel
{
    public ObservableCollection<PlayerAchievementItem> Achievements { get; } = [];

    [ObservableProperty] public partial string AchievementsSummaryText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasAchievementItems { get; set; }

    private async Task LoadAchievementsAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        var gameId = GameId;
        try
        {
            var result = await _api.GetGameAchievementsAsync(gameId, Loc.IsEnglish ? "en" : null);
            if (gameId != GameId)
            {
                return;
            }

            Achievements.Clear();
            if (result is not null)
            {
                foreach (var item in result.Items)
                {
                    Achievements.Add(PlayerAchievementItem.FromDto(item, _apiBaseUrl));
                }
            }

            HasAchievementItems = Achievements.Count > 0;
            AchievementsSummaryText = result is null || result.Total == 0
                ? Loc.T("Ta gra nie ma jeszcze osiągnięć.")
                : Loc.F("Odblokowano {0} z {1} · {2} / {3} pkt", result.Unlocked, result.Total, result.PointsEarned, result.PointsTotal);
        }
        catch
        {
            // the tab simply stays empty when the API cannot be reached
        }
    }
}
