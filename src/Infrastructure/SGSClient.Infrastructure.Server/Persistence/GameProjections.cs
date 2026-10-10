using System.Linq.Expressions;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Packaging;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence;

/// <summary>
/// Single place that turns catalog entities into DTOs inside EF queries.
/// Add a new <see cref="GameDto"/> field here and every endpoint picks it up.
/// </summary>
public static class GameProjections
{
    /// <param name="includeDlcs">Also load published DLC rows (detail pages); list endpoints pass false.</param>
    public static Expression<Func<GameEntity, GameDto>> ToDto(bool includeDlcs = false) =>
        g => new GameDto
        {
            Id = g.Id,
            UserId = g.UserId,
            Title = g.Title,
            Symbol = g.Symbol,
            Description = g.Description,
            HardwareRequirements = g.HardwareRequirements,
            OtherInformation = g.OtherInformation,
            Genres = g.Genres.OrderBy(x => x.Id).Select(x => new SgsGameTaxonomy.TaxonomyItem(x.Symbol, x.Name)).ToList(),
            Engines = g.Engines.OrderBy(x => x.Id).Select(x => new SgsGameTaxonomy.TaxonomyItem(x.Symbol, x.Name)).ToList(),
            CoverUrl = g.CoverUrl,
            HeaderUrl = g.HeaderUrl,
            LogoUrl = g.LogoUrl,
            IntroUrl = g.IntroUrl,
            ScreenshotUrls = g.Screenshots
                .OrderBy(s => s.SortOrder)
                .Select(s => s.Url)
                .ToList(),
            Screenshots = g.Screenshots
                .OrderBy(s => s.SortOrder)
                .Select(s => new GameScreenshotDto { Id = s.Id, Url = s.Url })
                .ToList(),
            DraftP = g.DraftP,
            FeaturedP = g.FeaturedP,
            FeaturedOrder = g.FeaturedOrder,
            Kind = g.Kind,
            ParentGameId = g.ParentGameId,
            ParentSymbol = g.ParentGame != null ? g.ParentGame.Symbol : null,
            CreatedAtUtc = g.CreatedAtUtc,
            StudioName = g.StudioName,
            WebsiteUrl = g.WebsiteUrl,
            DiscordUrl = g.DiscordUrl,
            SupportUrl = g.SupportUrl,
            ReleaseStatus = g.ReleaseStatus,
            ReleaseDateUtc = g.ReleaseDateUtc,
            AgeRating = g.AgeRating,
            Features = g.Features,
            LanguagesCsv = g.LanguagesCsv,
            TagsCsv = g.TagsCsv,
            PackageSizeBytes = g.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.PackageSizeBytes)
                .FirstOrDefault(),
            Builds = g.Builds
                .OrderByDescending(b => b.IsDefault)
                .ThenBy(b => b.Platform)
                .Select(b => new GameBuildDto
                {
                    Id = b.Id,
                    GameId = b.GameId,
                    Platform = b.Platform,
                    Architecture = b.Architecture,
                    CurrentVersion = b.CurrentVersion,
                    ExeName = b.ExeName,
                    ZipLink = b.ZipLink,
                    IsDefault = b.IsDefault
                })
                .ToList(),
            Dlcs = includeDlcs && g.Kind == GameContentKind.Game
                ? g.Dlcs
                    .Where(d => d.Kind == GameContentKind.Dlc && !d.DraftP)
                    .OrderBy(d => d.Title)
                    .Select(d => new GameDlcDto
                    {
                        Id = d.Id,
                        ParentGameId = d.ParentGameId ?? g.Id,
                        Title = d.Title,
                        Symbol = d.Symbol,
                        CoverUrl = d.CoverUrl,
                        PackageSizeBytes = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.PackageSizeBytes)
                            .FirstOrDefault(),
                        CreatedAtUtc = d.CreatedAtUtc,
                        PreferredBuildId = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.Id)
                            .FirstOrDefault(),
                        PreferredPlatform = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.Platform)
                            .FirstOrDefault(),
                        PreferredArchitecture = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.Architecture)
                            .FirstOrDefault(),
                        PreferredVersion = d.Builds
                            .OrderByDescending(b => b.IsDefault)
                            .Select(b => b.CurrentVersion)
                            .FirstOrDefault()
                    })
                    .ToList()
                : new List<GameDlcDto>()
        };

    public static Expression<Func<GameEntity, GameDlcDto>> ToDlcDto() =>
        d => new GameDlcDto
        {
            Id = d.Id,
            ParentGameId = d.ParentGameId ?? 0,
            Title = d.Title,
            Symbol = d.Symbol,
            CoverUrl = d.CoverUrl,
            PackageSizeBytes = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.PackageSizeBytes)
                .FirstOrDefault(),
            CreatedAtUtc = d.CreatedAtUtc,
            PreferredBuildId = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.Id)
                .FirstOrDefault(),
            PreferredPlatform = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.Platform)
                .FirstOrDefault(),
            PreferredArchitecture = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.Architecture)
                .FirstOrDefault(),
            PreferredVersion = d.Builds
                .OrderByDescending(b => b.IsDefault)
                .Select(b => b.CurrentVersion)
                .FirstOrDefault()
        };
}
