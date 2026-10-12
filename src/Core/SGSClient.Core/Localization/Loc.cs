using System.Globalization;
using System.Text.RegularExpressions;

namespace SGSClient.Core.Localization;

/// <summary>
/// UI text lookup. Source strings are Polish; English comes from <c>Loc.En.cs</c>.
/// A missing entry falls back to the Polish source, so untranslated text never breaks.
/// </summary>
public static partial class Loc
{
    public static bool IsEnglish { get; private set; }

    /// <summary>Culture matching the UI language, for dates and numbers.</summary>
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "pl-PL");

    /// <summary>All English entries; exposed so tests can check placeholder consistency.</summary>
    public static IReadOnlyDictionary<string, string> Entries => English;

    /// <summary>"pl", "en", or null / "system" to follow the OS display language.</summary>
    public static void Initialize(string? language)
    {
        IsEnglish = language switch
        {
            "pl" => false,
            "en" => true,
            _ => !string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "pl", StringComparison.OrdinalIgnoreCase)
        };

        var culture = CultureInfo.GetCultureInfo(IsEnglish ? "en-US" : "pl-PL");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public static string T(string pl)
    {
        if (!IsEnglish)
        {
            return pl;
        }

        var core = pl.Trim();
        if (!English.TryGetValue(core, out var en))
        {
            return pl;
        }

        var lead = pl.Length - pl.TrimStart().Length;
        var trail = pl.Length - pl.TrimEnd().Length;
        return pl[..lead] + en + pl[(pl.Length - trail)..];
    }

    private static readonly Regex Placeholder = new(@"\{(\d+)(?::[^}]*)?\}");

    private static readonly Lazy<(Regex Pattern, string En, int Count)[]> Patterns = new(() =>
        English
            .Where(kv => Placeholder.IsMatch(kv.Key))
            .Select(kv =>
            {
                var max = Placeholder.Matches(kv.Key).Max(m => int.Parse(m.Groups[1].Value));
                var rx = Placeholder.Replace(Regex.Escape(kv.Key).Replace(@"\{", "{"), m => "(?<g" + m.Groups[1].Value + ">.+?)");
                return (new Regex("^" + rx + "$", RegexOptions.Singleline), kv.Value, max + 1);
            })
            .ToArray());

    /// <summary>
    /// Translates a message produced by the server, which is always Polish. Tries an exact entry first,
    /// then entries with {0} placeholders (the captured values are translated too, e.g. "gatunków").
    /// </summary>
    public static string TS(string? serverMessage)
    {
        if (string.IsNullOrEmpty(serverMessage) || !IsEnglish)
        {
            return serverMessage ?? string.Empty;
        }

        var exact = T(serverMessage);
        if (!ReferenceEquals(exact, serverMessage))
        {
            return exact;
        }

        foreach (var (pattern, en, count) in Patterns.Value)
        {
            var m = pattern.Match(serverMessage);
            if (!m.Success)
            {
                continue;
            }

            var args = new object[count];
            for (var i = 0; i < count; i++)
            {
                args[i] = T(m.Groups["g" + i].Value);
            }

            return string.Format(CultureInfo.CurrentCulture, en, args);
        }

        return serverMessage;
    }

    /// <summary>Format string lookup: <paramref name="pl"/> uses {0}, {1}… placeholders.</summary>
    public static string F(string pl, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(pl), args);
}
