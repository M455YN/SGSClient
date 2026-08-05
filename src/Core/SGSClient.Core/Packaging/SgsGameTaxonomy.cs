namespace SGSClient.Core.Packaging;

/// <summary>
/// Canonical genre/engine slugs shared by manifest validation, DB seed, and publish UI.
/// </summary>
public static class SgsGameTaxonomy
{
    public static readonly IReadOnlyList<TaxonomyItem> Genres =
    [
        new("action", "Akcja"),
        new("adventure", "Przygodowa"),
        new("rpg", "RPG"),
        new("strategy", "Strategia"),
        new("simulation", "Symulacja"),
        new("sports", "Sportowa"),
        new("racing", "Wyścigowa"),
        new("puzzle", "Logiczna"),
        new("horror", "Horror"),
        new("indie", "Indie"),
        new("other", "Inna")
    ];

    public static readonly IReadOnlyList<TaxonomyItem> Engines =
    [
        new("unity", "Unity"),
        new("unreal", "Unreal Engine"),
        new("godot", "Godot"),
        new("cryengine", "CryEngine"),
        new("custom", "Własny / inny"),
        new("other", "Inny")
    ];

    public static bool IsValidGenre(string? symbol) =>
        !string.IsNullOrWhiteSpace(symbol)
        && Genres.Any(g => string.Equals(g.Symbol, symbol.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsValidEngine(string? symbol) =>
        !string.IsNullOrWhiteSpace(symbol)
        && Engines.Any(e => string.Equals(e.Symbol, symbol.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? NormalizeSlug(string? symbol) =>
        string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim().ToLowerInvariant();

    public readonly record struct TaxonomyItem(string Symbol, string Name);
}
