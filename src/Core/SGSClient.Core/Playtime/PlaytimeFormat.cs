using System.Globalization;

namespace SGSClient.Core.Playtime;

public static class PlaytimeFormat
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>Short label, e.g. "45 min" or "2,4 godz.".</summary>
    public static string ToPolish(long totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return "mniej niż 1 min";
        }

        if (totalSeconds < 3600)
        {
            var minutes = totalSeconds / 60;
            return $"{minutes} min";
        }

        var hours = totalSeconds / 3600d;
        return hours < 10
            ? $"{hours.ToString("0.0", Polish)} godz."
            : $"{(long)hours} godz.";
    }

    /// <summary>Library-style label, e.g. "2,4 godz. łącznie".</summary>
    public static string ToPolishTotal(long totalSeconds) => $"{ToPolish(totalSeconds)} łącznie";
}
