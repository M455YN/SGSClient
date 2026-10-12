using SGSClient.Core.Localization;
using System.Text.RegularExpressions;

namespace SGSClient.Core.Auth;

public static partial class PlayerTagRules
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "discord", "everyone", "here", "me", "null",
        "official", "sgs", "sgsclient", "support", "system"
    };

    public static bool TryNormalize(string? raw, out string tag, out string error)
    {
        tag = string.Empty;
        var value = (raw ?? string.Empty).Trim().TrimStart('@').ToLowerInvariant();
        if (value.Length < MinLength || value.Length > MaxLength)
        {
            error = Loc.F("Tag musi mieć {0}–{1} znaków.", MinLength, MaxLength);
            return false;
        }

        if (!TagPattern().IsMatch(value))
        {
            error = Loc.T("Dozwolone: litery, cyfry, podkreślenie i kropka. Zaczyna się literą lub cyfrą.");
            return false;
        }

        if (value.Contains("..", StringComparison.Ordinal) || value.StartsWith('.') || value.EndsWith('.'))
        {
            error = Loc.T("Kropka nie może być na początku, na końcu ani podwójna.");
            return false;
        }

        if (Reserved.Contains(value))
        {
            error = Loc.T("Ten tag jest zarezerwowany.");
            return false;
        }

        tag = value;
        error = string.Empty;
        return true;
    }

    public static string? SuggestFrom(string? displayName, string? email)
    {
        var source = displayName;
        if (string.IsNullOrWhiteSpace(source) || source.Equals("Player", StringComparison.OrdinalIgnoreCase))
        {
            var at = email?.IndexOf('@') ?? -1;
            source = at > 0 ? email![..at] : displayName;
        }

        var chars = (source ?? "gracz")
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' ? c : '_')
            .ToArray();
        var compact = new string(chars).Trim('_', '.');
        while (compact.Contains("..", StringComparison.Ordinal))
        {
            compact = compact.Replace("..", ".", StringComparison.Ordinal);
        }

        if (compact.Length < MinLength)
        {
            compact = string.Concat(compact, "gracz");
        }

        if (compact.Length > MaxLength)
        {
            compact = compact[..MaxLength].TrimEnd('.', '_');
        }

        if (compact.Length < MinLength || !char.IsAsciiLetterOrDigit(compact[0]))
        {
            compact = "gracz";
        }

        return TryNormalize(compact, out var tag, out _) ? tag : null;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._]{2,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();
}
