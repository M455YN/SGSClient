using SGSClient.Core.Localization;
using SGSClient.Core.DTOs;

namespace SGSClient.Core.Enums;

/// <summary>Polish store labels for creator metadata shown in Studio and the catalog.</summary>
public static class GameStoreLabels
{
    public static IReadOnlyList<string> ReleaseStatusOptions { get; } =
        [Loc.T("Wkrótce"), Loc.T("Wczesny dostęp"), Loc.T("Pełna wersja"), "Demo"];

    public static IReadOnlyList<string> AgeRatingOptions { get; } =
        [Loc.T("Bez kategorii"), "PEGI 3", "PEGI 7", "PEGI 12", "PEGI 16", "PEGI 18"];

    public static string ReleaseStatus(GameReleaseStatus status) => status switch
    {
        GameReleaseStatus.Announced => ReleaseStatusOptions[0],
        GameReleaseStatus.EarlyAccess => ReleaseStatusOptions[1],
        GameReleaseStatus.Demo => ReleaseStatusOptions[3],
        _ => ReleaseStatusOptions[2]
    };

    public static int ReleaseStatusIndex(GameReleaseStatus status) => status switch
    {
        GameReleaseStatus.Announced => 0,
        GameReleaseStatus.EarlyAccess => 1,
        GameReleaseStatus.Demo => 3,
        _ => 2
    };

    public static GameReleaseStatus ReleaseStatusFromIndex(int index) => index switch
    {
        0 => GameReleaseStatus.Announced,
        1 => GameReleaseStatus.EarlyAccess,
        3 => GameReleaseStatus.Demo,
        _ => GameReleaseStatus.Released
    };

    public static string AgeRating(GameAgeRating rating) => rating switch
    {
        GameAgeRating.Pegi3 => AgeRatingOptions[1],
        GameAgeRating.Pegi7 => AgeRatingOptions[2],
        GameAgeRating.Pegi12 => AgeRatingOptions[3],
        GameAgeRating.Pegi16 => AgeRatingOptions[4],
        GameAgeRating.Pegi18 => AgeRatingOptions[5],
        _ => AgeRatingOptions[0]
    };

    public static int AgeRatingIndex(GameAgeRating rating) => rating switch
    {
        GameAgeRating.Pegi3 => 1,
        GameAgeRating.Pegi7 => 2,
        GameAgeRating.Pegi12 => 3,
        GameAgeRating.Pegi16 => 4,
        GameAgeRating.Pegi18 => 5,
        _ => 0
    };

    public static GameAgeRating AgeRatingFromIndex(int index) => index switch
    {
        1 => GameAgeRating.Pegi3,
        2 => GameAgeRating.Pegi7,
        3 => GameAgeRating.Pegi12,
        4 => GameAgeRating.Pegi16,
        5 => GameAgeRating.Pegi18,
        _ => GameAgeRating.Unrated
    };

    public static string Feature(GameFeatures feature) => feature switch
    {
        GameFeatures.SinglePlayer => "Singleplayer",
        GameFeatures.MultiPlayer => "Multiplayer",
        GameFeatures.CoOp => Loc.T("Kooperacja"),
        GameFeatures.ControllerSupport => "Pad",
        GameFeatures.CloudSaves => Loc.T("Zapisy w chmurze"),
        GameFeatures.Achievements => Loc.T("Osiągnięcia"),
        GameFeatures.Modding => Loc.T("Mody"),
        GameFeatures.Vr => "VR",
        _ => feature.ToString()
    };

    public static IReadOnlyList<GameFeatures> FeatureFlags { get; } =
    [
        GameFeatures.SinglePlayer,
        GameFeatures.MultiPlayer,
        GameFeatures.CoOp,
        GameFeatures.ControllerSupport,
        GameFeatures.CloudSaves,
        GameFeatures.Achievements,
        GameFeatures.Modding,
        GameFeatures.Vr
    ];

    public static IReadOnlyList<string> FeatureList(GameFeatures features) =>
        FeatureFlags.Where(flag => features.HasFlag(flag)).Select(Feature).ToList();

    public static IReadOnlyList<string> CatalogBadges(GameDto game)
    {
        var badges = new List<string>();
        if (game.FeaturedP)
        {
            badges.Add(Loc.T("Wyróżnione"));
        }

        if (game.ReleaseStatus != GameReleaseStatus.Released)
        {
            badges.Add(ReleaseStatus(game.ReleaseStatus));
        }

        if (game.AgeRating != GameAgeRating.Unrated)
        {
            badges.Add(AgeRating(game.AgeRating));
        }

        badges.AddRange(game.Genres.Select(g => g.Name));

        badges.AddRange(game.Tags.Take(4));
        badges.AddRange(FeatureList(game.Features).Take(3));
        return badges;
    }
}
