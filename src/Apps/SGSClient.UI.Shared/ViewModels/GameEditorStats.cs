using System.Collections.ObjectModel;
using System.Globalization;
using SGSClient.Core.DTOs;

namespace SGSClient.UI.ViewModels;

public static class GameEditorStats
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static void Apply(
        GameStatsDto? stats,
        ObservableCollection<StudioStatBar> bars,
        out string total,
        out string last30,
        out string last7,
        out string lastDownload,
        out bool hasBars)
    {
        total = (stats?.TotalDownloads ?? 0).ToString("N0", Polish);
        last30 = (stats?.DownloadsLast30Days ?? 0).ToString("N0", Polish);
        last7 = (stats?.DownloadsLast7Days ?? 0).ToString("N0", Polish);
        lastDownload = stats?.LastDownloadUtc is { } last
            ? last.LocalDateTime.ToString("d MMM yyyy HH:mm", Polish)
            : "brak";

        bars.Clear();
        if (stats is null || stats.Daily.Count == 0)
        {
            hasBars = false;
            return;
        }

        var max = stats.Daily.Max(p => p.Downloads);
        foreach (var point in stats.Daily)
        {
            bars.Add(new StudioStatBar
            {
                Label = point.Day.ToString("d.MM"),
                Value = point.Downloads,
                Max = Math.Max(1, max)
            });
        }

        hasBars = bars.Count > 0;
    }
}
