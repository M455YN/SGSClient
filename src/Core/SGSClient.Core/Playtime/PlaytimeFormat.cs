using SGSClient.Core.Localization;
using System.Globalization;

namespace SGSClient.Core.Playtime;

public static class PlaytimeFormat
{
    private static readonly CultureInfo Polish = Loc.Culture;

    /// <summary>Short label, e.g. "45 min" or "2,4 godz.".</summary>
    public static string ToPolish(long totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return Loc.T("mniej niż 1 min");
        }

        if (totalSeconds < 3600)
        {
            var minutes = totalSeconds / 60;
            return $"{minutes} min";
        }

        var hours = totalSeconds / 3600d;
        return hours < 10
            ? $"{hours.ToString("0.0", Polish)} {Loc.T("godz.")}"
            : $"{(long)hours} {Loc.T("godz.")}";
    }

    /// <summary>Library-style label, e.g. "2,4 godz. łącznie".</summary>
    public static string ToPolishTotal(long totalSeconds) => Loc.F("{0} łącznie", ToPolish(totalSeconds));
}
