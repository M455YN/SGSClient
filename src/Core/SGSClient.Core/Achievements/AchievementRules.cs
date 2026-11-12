using System.Text.RegularExpressions;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Localization;
using SGSClient.Core.Packaging;

namespace SGSClient.Core.Achievements;

/// <summary>Limits shared by the Studio panel (preflight), the manifest and the server (enforcement).</summary>
public static partial class AchievementRules
{
    public const int MaxPerGame = 500;
    public const int MaxKeyLength = 64;
    public const int MaxTitleLength = 128;
    public const int MaxDescriptionLength = 512;
    public const int MaxPoints = 1000;
    public const int MinTarget = 2;
    public const int MaxTarget = 1_000_000;
    public const int MaxTranslations = 10;
    public const int MaxStatsPerGame = 100;
    public const int MaxStatNameLength = 128;

    /// <summary>Folder inside the game's storage (and media URL) that holds achievement icons.</summary>
    public const string IconFolder = "sdk_sgs/achievements";

    [GeneratedRegex("^[a-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[a-z]{2,3}(-[a-z0-9]{2,8})?$", RegexOptions.CultureInvariant)]
    private static partial Regex LanguagePattern();

    /// <summary>Trims and lowercases a key; null when it is empty or not a valid key.</summary>
    public static string? NormalizeKey(string? key)
    {
        var normalized = key?.Trim().ToLowerInvariant();
        return normalized is not null && KeyPattern().IsMatch(normalized) ? normalized : null;
    }

    /// <summary>
    /// API name proposed for a display name: "Pogromca bossa!" → "ach_pogromca_bossa". Diacritics are
    /// dropped, everything else that is not a letter or digit becomes "_". Empty for an empty name.
    /// </summary>
    public static string SuggestKey(string? title)
    {
        var builder = new System.Text.StringBuilder("ach_");
        foreach (var c in (title ?? string.Empty).Replace('ł', 'l').Replace('Ł', 'L').Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        var key = builder.ToString().TrimEnd('_');
        return key.Length <= "ach".Length ? string.Empty : key[..Math.Min(key.Length, MaxKeyLength)].TrimEnd('_');
    }

    /// <summary>Trims and lowercases a language code ("EN " → "en"); null when it is not one.</summary>
    public static string? NormalizeLanguage(string? language)
    {
        var normalized = language?.Trim().ToLowerInvariant();
        return normalized is not null && LanguagePattern().IsMatch(normalized) ? normalized : null;
    }

    /// <summary>An icon is an http(s) URL or an app-relative path to an uploaded file.</summary>
    public static bool TryNormalizeIconUrl(string? value, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return true;
        }

        if (trimmed.StartsWith('/'))
        {
            if (trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.Contains("..", StringComparison.Ordinal)
                || trimmed.Length > SgsPackageRules.MaxUrlLength)
            {
                error = Loc.T("Adres ikony jest nieprawidłowy.");
                return false;
            }

            normalized = trimmed;
            return true;
        }

        return SgsPackageRules.TryNormalizeHttpUrl(trimmed, out normalized, out error);
    }

    /// <summary>
    /// First problem found in a full set of definitions, or null when it is valid. <paramref name="statKeys"/>
    /// are the game's stat API names; null skips the check that a linked stat exists.
    /// </summary>
    public static string? Validate(IReadOnlyList<AchievementDefinitionDto> items, IReadOnlyCollection<string>? statKeys = null)
    {
        if (items.Count > MaxPerGame)
        {
            return Loc.F("Maksymalnie {0} osiągnięć na grę.", MaxPerGame);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var key = NormalizeKey(item.Key);
            if (key is null)
            {
                return Loc.F("Klucz „{0}” jest nieprawidłowy (małe litery, cyfry, _ i -, do 64 znaków).", item.Key);
            }

            if (!seen.Add(key))
            {
                return Loc.F("Klucz „{0}” występuje więcej niż raz.", key);
            }

            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Trim().Length > MaxTitleLength)
            {
                return Loc.F("Osiągnięcie „{0}”: tytuł jest wymagany (do {1} znaków).", key, MaxTitleLength);
            }

            if ((item.Description?.Trim().Length ?? 0) > MaxDescriptionLength)
            {
                return Loc.F("Osiągnięcie „{0}”: opis jest za długi (do {1} znaków).", key, MaxDescriptionLength);
            }

            if (item.Points is < 0 or > MaxPoints)
            {
                return Loc.F("Osiągnięcie „{0}”: punkty muszą być w zakresie 0–{1}.", key, MaxPoints);
            }

            if (item.Target is < MinTarget or > MaxTarget)
            {
                return Loc.F("Osiągnięcie „{0}”: cel licznika musi być w zakresie {1}–{2}.", key, MinTarget, MaxTarget);
            }

            if (!TryNormalizeIconUrl(item.IconUrl, out _, out var urlError)
                || !TryNormalizeIconUrl(item.LockedIconUrl, out _, out urlError))
            {
                return Loc.F("Osiągnięcie „{0}”: {1}", key, urlError);
            }

            if (!string.IsNullOrWhiteSpace(item.StatKey))
            {
                var stat = NormalizeKey(item.StatKey);
                if (stat is null || (statKeys is not null && !statKeys.Contains(stat)))
                {
                    return Loc.F("Osiągnięcie „{0}”: statystyka „{1}” nie istnieje.", key, item.StatKey);
                }

                if (item.Target is null)
                {
                    return Loc.F("Osiągnięcie „{0}”: osiągnięcie ze statystyką potrzebuje celu.", key);
                }
            }

            if (ValidateTranslations(key, item.Translations) is { } translationError)
            {
                return translationError;
            }
        }

        return null;
    }

    /// <summary>First problem found in a full set of stat definitions, or null when it is valid.</summary>
    public static string? ValidateStats(IReadOnlyList<GameStatDefinitionDto> stats)
    {
        if (stats.Count > MaxStatsPerGame)
        {
            return Loc.F("Maksymalnie {0} statystyk na grę.", MaxStatsPerGame);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stat in stats)
        {
            var key = NormalizeKey(stat.Key);
            if (key is null)
            {
                return Loc.F("Klucz statystyki „{0}” jest nieprawidłowy (małe litery, cyfry, _ i -, do 64 znaków).", stat.Key);
            }

            if (!seen.Add(key))
            {
                return Loc.F("Klucz statystyki „{0}” występuje więcej niż raz.", key);
            }

            if ((stat.DisplayName?.Trim().Length ?? 0) > MaxStatNameLength)
            {
                return Loc.F("Statystyka „{0}”: nazwa jest za długa (do {1} znaków).", key, MaxStatNameLength);
            }

            if (!Enum.IsDefined(stat.Type))
            {
                return Loc.F("Statystyka „{0}”: nieznany typ.", key);
            }

            double?[] numbers = [stat.MinValue, stat.MaxValue, stat.DefaultValue, stat.MaxChange];
            if (numbers.Any(n => n is double d && !IsValidNumber(stat.Type, d)))
            {
                return Loc.F("Statystyka „{0}”: wartości muszą być liczbami (całkowitymi dla typu INT).", key);
            }

            if (stat.MinValue > stat.MaxValue)
            {
                return Loc.F("Statystyka „{0}”: minimum jest większe niż maksimum.", key);
            }

            if (stat.DefaultValue < stat.MinValue || stat.DefaultValue > stat.MaxValue)
            {
                return Loc.F("Statystyka „{0}”: wartość domyślna wykracza poza zakres.", key);
            }

            if (stat.MaxChange is <= 0)
            {
                return Loc.F("Statystyka „{0}”: maksymalna zmiana musi być większa od zera.", key);
            }
        }

        return null;
    }

    /// <summary>
    /// Why a game may not move a stat from <paramref name="current"/> to <paramref name="value"/>, or null
    /// when the write is allowed. Like Steam, a write that breaks a constraint is rejected, not clamped.
    /// </summary>
    public static string? CheckStatValue(GameStatDefinitionDto stat, double current, double value)
    {
        if (!IsValidNumber(stat.Type, value))
        {
            return stat.Type == GameStatType.Int
                ? Loc.F("Statystyka „{0}” przyjmuje tylko liczby całkowite.", stat.Key)
                : Loc.F("Statystyka „{0}”: wartość nie jest liczbą.", stat.Key);
        }

        if (value < stat.MinValue || value > stat.MaxValue)
        {
            return Loc.F("Statystyka „{0}”: wartość wykracza poza zakres.", stat.Key);
        }

        if (stat.IncrementOnly && value < current)
        {
            return Loc.F("Statystyka „{0}” może tylko rosnąć.", stat.Key);
        }

        if (stat.MaxChange is double maxChange && Math.Abs(value - current) > maxChange)
        {
            return Loc.F("Statystyka „{0}”: zmiana jest większa niż dozwolone {1}.", stat.Key, maxChange);
        }

        return null;
    }

    /// <summary>Achievement progress for a stat value: floored, clamped to 0…target; unlocked once it reaches the target.</summary>
    public static (int Progress, bool Reached) StatProgress(double value, int target) =>
        ((int)Math.Clamp(Math.Floor(value), 0, target), value >= target);

    private static bool IsValidNumber(GameStatType type, double value) =>
        double.IsFinite(value)
        && (type != GameStatType.Int || (value == Math.Floor(value) && value is >= int.MinValue and <= int.MaxValue));

    private static string? ValidateTranslations(string key, IReadOnlyDictionary<string, AchievementTextDto>? translations)
    {
        if (translations is null)
        {
            return null;
        }

        if (translations.Count > MaxTranslations)
        {
            return Loc.F("Osiągnięcie „{0}”: za dużo tłumaczeń (maksymalnie {1}).", key, MaxTranslations);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (code, text) in translations)
        {
            var language = NormalizeLanguage(code);
            if (language is null || !seen.Add(language))
            {
                return Loc.F("Osiągnięcie „{0}”: kod języka „{1}” jest nieprawidłowy lub się powtarza.", key, code);
            }

            if ((text?.Title?.Trim().Length ?? 0) > MaxTitleLength
                || (text?.Description?.Trim().Length ?? 0) > MaxDescriptionLength)
            {
                return Loc.F("Osiągnięcie „{0}”: tłumaczenie „{1}” jest za długie.", key, language);
            }
        }

        return null;
    }
}
