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
        new("platformer", "Platformówka"),
        new("shooter", "Strzelanka"),
        new("fps", "FPS"),
        new("tps", "TPS"),
        new("fighting", "Bijatyka"),
        new("stealth", "Stealth"),
        new("survival", "Survival"),
        new("sandbox", "Sandbox"),
        new("open-world", "Otwarty świat"),
        new("roguelike", "Roguelike"),
        new("roguelite", "Roguelite"),
        new("metroidvania", "Metroidvania"),
        new("visual-novel", "Visual novel"),
        new("card", "Karciana"),
        new("turn-based", "Turowe"),
        new("mmo", "MMO"),
        new("moba", "MOBA"),
        new("battle-royale", "Battle royale"),
        new("tower-defense", "Tower defense"),
        new("idle", "Idle / incremental"),
        new("casual", "Casual"),
        new("party", "Imprezowa"),
        new("rhythm", "Rytmiczna"),
        new("educational", "Edukacyjna"),
        new("narrative", "Narracyjna"),
        new("walking-sim", "Walking simulator"),
        new("vr", "VR"),
        new("other", "Inna")
    ];

    public static readonly IReadOnlyList<TaxonomyItem> Engines =
    [
        new("unity", "Unity"),
        new("unreal", "Unreal Engine"),
        new("godot", "Godot"),
        new("cryengine", "CryEngine"),
        new("gamemaker", "GameMaker"),
        new("construct", "Construct"),
        new("rpgmaker", "RPG Maker"),
        new("renpy", "Ren'Py"),
        new("defold", "Defold"),
        new("monogame", "MonoGame"),
        new("fna", "FNA"),
        new("stride", "Stride"),
        new("bevy", "Bevy"),
        new("libgdx", "libGDX"),
        new("love2d", "LÖVE (Love2D)"),
        new("phaser", "Phaser"),
        new("cocos", "Cocos"),
        new("solar2d", "Solar2D"),
        new("clickteam-fusion", "Clickteam Fusion"),
        new("source", "Source"),
        new("source2", "Source 2"),
        new("idtech", "id Tech"),
        new("frostbite", "Frostbite"),
        new("creation-engine", "Creation Engine"),
        new("red-engine", "REDengine"),
        new("anvil", "Anvil"),
        new("snowdrop", "Snowdrop"),
        new("o3de", "O3DE"),
        new("raylib", "raylib"),
        new("pygame", "Pygame"),
        new("sdl", "SDL"),
        new("html5", "HTML5 / Web"),
        new("roblox", "Roblox"),
        new("custom", "Własny / inny"),
        new("other", "Inny")
    ];

    public static bool IsValidGenre(string? symbol) =>
        !string.IsNullOrWhiteSpace(symbol)
        && Genres.Any(g => string.Equals(g.Symbol, symbol.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool IsValidEngine(string? symbol) =>
        !string.IsNullOrWhiteSpace(symbol)
        && Engines.Any(e => string.Equals(e.Symbol, symbol.Trim(), StringComparison.OrdinalIgnoreCase));

    public const int MaxGenres = 5;
    public const int MaxEngines = 3;

    /// <summary>
    /// Merges a legacy single slug with a list, lowercases, drops blanks and duplicates (order kept).
    /// Does not validate against the taxonomy.
    /// </summary>
    public static List<string> MergeSlugs(string? single, IEnumerable<string>? many)
    {
        var result = new List<string>();
        foreach (var raw in (many ?? []).Prepend(single!))
        {
            var slug = NormalizeSlug(raw);
            if (slug is not null && !result.Contains(slug))
            {
                result.Add(slug);
            }
        }

        return result;
    }

    public static string? NormalizeSlug(string? symbol) =>
        string.IsNullOrWhiteSpace(symbol) ? null : symbol.Trim().ToLowerInvariant();

    public readonly record struct TaxonomyItem(string Symbol, string Name);
}
