using System.Text.RegularExpressions;
using SGSClient.Core.DTOs;
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

    /// <summary>First problem found in a full set of definitions, or null when it is valid.</summary>
    public static string? Validate(IReadOnlyList<AchievementDefinitionDto> items)
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

            if (!TryNormalizeIconUrl(item.IconUrl, out _, out var urlError))
            {
                return Loc.F("Osiągnięcie „{0}”: {1}", key, urlError);
            }

            if (ValidateTranslations(key, item.Translations) is { } translationError)
            {
                return translationError;
            }
        }

        return null;
    }

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
