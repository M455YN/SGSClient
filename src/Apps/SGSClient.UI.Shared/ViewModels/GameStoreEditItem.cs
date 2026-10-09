using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Packaging;

namespace SGSClient.UI.ViewModels;

public abstract partial class GameStoreEditItem : ObservableObject
{
    [ObservableProperty] public partial string Title { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial string? HardwareRequirements { get; set; }
    [ObservableProperty] public partial string? OtherInformation { get; set; }
    [ObservableProperty] public partial string? GenreNamesText { get; set; }
    [ObservableProperty] public partial string? EngineNamesText { get; set; }
    [ObservableProperty] public partial string? IntroUrl { get; set; }
    [ObservableProperty] public partial string? CoverImageUrl { get; set; }
    [ObservableProperty] public partial string? HeaderImageUrl { get; set; }
    [ObservableProperty] public partial string? OwnCoverImageUrl { get; set; }
    [ObservableProperty] public partial string? OwnHeaderImageUrl { get; set; }
    [ObservableProperty] public partial string? OwnLogoImageUrl { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPublished))]
    [NotifyPropertyChangedFor(nameof(DraftLabel))]
    public partial bool DraftP { get; set; }

    [ObservableProperty] public partial bool FeaturedP { get; set; }
    [ObservableProperty] public partial string? StudioName { get; set; }
    [ObservableProperty] public partial string? WebsiteUrl { get; set; }
    [ObservableProperty] public partial string? DiscordUrl { get; set; }
    [ObservableProperty] public partial string? SupportUrl { get; set; }
    [ObservableProperty] public partial GameReleaseStatus ReleaseStatus { get; set; }
    [ObservableProperty] public partial DateTimeOffset? ReleaseDateUtc { get; set; }
    [ObservableProperty] public partial GameAgeRating AgeRating { get; set; }
    [ObservableProperty] public partial GameFeatures Features { get; set; }
    [ObservableProperty] public partial string CurrentVersionText { get; set; } = "—";
    [ObservableProperty] public partial string EditTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditHardwareRequirements { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditOtherInformation { get; set; } = string.Empty;
    public TaxonomySelection EditGenres { get; } =
        new(SgsGameTaxonomy.Genres, SgsGameTaxonomy.MaxGenres, "Wybierz gatunki");
    public TaxonomySelection EditEngines { get; } =
        new(SgsGameTaxonomy.Engines, SgsGameTaxonomy.MaxEngines, "Wybierz silniki");
    [ObservableProperty] public partial string EditStudioName { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditWebsiteUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDiscordUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditSupportUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial int EditReleaseStatusIndex { get; set; } = 2;
    [ObservableProperty] public partial DateTimeOffset? EditReleaseDate { get; set; }
    [ObservableProperty] public partial int EditAgeRatingIndex { get; set; }
    [ObservableProperty] public partial string EditLanguages { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditTags { get; set; } = string.Empty;
    [ObservableProperty] public partial bool HasSinglePlayer { get; set; }
    [ObservableProperty] public partial bool HasMultiPlayer { get; set; }
    [ObservableProperty] public partial bool HasCoOp { get; set; }
    [ObservableProperty] public partial bool HasController { get; set; }
    [ObservableProperty] public partial bool HasCloudSaves { get; set; }
    [ObservableProperty] public partial bool HasAchievements { get; set; }
    [ObservableProperty] public partial bool HasModding { get; set; }
    [ObservableProperty] public partial bool HasVr { get; set; }

    public ObservableCollection<StudioScreenshotItem> ScreenshotItems { get; } = [];
    public ObservableCollection<StudioBuildItem> Builds { get; } = [];

    public int Id { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public bool IsDlc { get; init; }

    public bool HasCover => !string.IsNullOrWhiteSpace(CoverImageUrl);
    public bool HasHeaderArt => !string.IsNullOrWhiteSpace(HeaderImageUrl);
    public bool HasLogoArt => !string.IsNullOrWhiteSpace(OwnLogoImageUrl);
    public bool HasIntro => !string.IsNullOrWhiteSpace(IntroUrl);
    public bool IsPublished => !DraftP;
    public string DraftLabel => DraftP ? "Szkic" : "W sklepie";
    public string KindLabel => IsDlc ? "DLC" : "Gra";
    public string TileLabel => string.IsNullOrWhiteSpace(Symbol)
        ? Title[..Math.Min(2, Title.Length)].ToUpperInvariant()
        : Symbol[..Math.Min(3, Symbol.Length)].ToUpperInvariant();

    public GameFeatures EditedFeatures
    {
        get
        {
            var value = GameFeatures.None;
            if (HasSinglePlayer) value |= GameFeatures.SinglePlayer;
            if (HasMultiPlayer) value |= GameFeatures.MultiPlayer;
            if (HasCoOp) value |= GameFeatures.CoOp;
            if (HasController) value |= GameFeatures.ControllerSupport;
            if (HasCloudSaves) value |= GameFeatures.CloudSaves;
            if (HasAchievements) value |= GameFeatures.Achievements;
            if (HasModding) value |= GameFeatures.Modding;
            if (HasVr) value |= GameFeatures.Vr;
            return value;
        }
    }

    public UpdateGameRequestDto ToUpdateRequest() => new()
    {
        Title = string.IsNullOrWhiteSpace(EditTitle) ? Title : EditTitle.Trim(),
        Description = EditDescription,
        HardwareRequirements = EditHardwareRequirements,
        OtherInformation = EditOtherInformation,
        Genres = EditGenres.SelectedSymbols,
        Engines = EditEngines.SelectedSymbols,
        DraftP = DraftP,
        StudioName = EditStudioName,
        WebsiteUrl = EditWebsiteUrl,
        DiscordUrl = EditDiscordUrl,
        SupportUrl = EditSupportUrl,
        ReleaseStatus = GameStoreLabels.ReleaseStatusFromIndex(EditReleaseStatusIndex),
        ReleaseDateUtc = EditReleaseDate,
        ClearReleaseDate = EditReleaseDate is null,
        AgeRating = GameStoreLabels.AgeRatingFromIndex(EditAgeRatingIndex),
        Features = EditedFeatures,
        Languages = CsvList.Split(EditLanguages),
        Tags = CsvList.Split(EditTags)
    };

    protected void ApplyStoreEdit(GameDto game)
    {
        Title = game.Title;
        Description = game.Description;
        HardwareRequirements = game.HardwareRequirements;
        OtherInformation = game.OtherInformation;
        GenreNamesText = game.GenreNamesText;
        EngineNamesText = game.EngineNamesText;
        IntroUrl = game.IntroUrl;
        DraftP = game.DraftP;
        FeaturedP = game.FeaturedP;
        StudioName = game.StudioName;
        WebsiteUrl = game.WebsiteUrl;
        DiscordUrl = game.DiscordUrl;
        SupportUrl = game.SupportUrl;
        ReleaseStatus = game.ReleaseStatus;
        ReleaseDateUtc = game.ReleaseDateUtc;
        AgeRating = game.AgeRating;
        Features = game.Features;
        CurrentVersionText = game.Builds.FirstOrDefault()?.CurrentVersion ?? "—";
        EditTitle = game.Title;
        EditDescription = game.Description ?? string.Empty;
        EditHardwareRequirements = game.HardwareRequirements ?? string.Empty;
        EditOtherInformation = game.OtherInformation ?? string.Empty;
        EditGenres.Set(game.Genres.Select(g => g.Symbol));
        EditEngines.Set(game.Engines.Select(e => e.Symbol));
        EditStudioName = game.StudioName ?? string.Empty;
        EditWebsiteUrl = game.WebsiteUrl ?? string.Empty;
        EditDiscordUrl = game.DiscordUrl ?? string.Empty;
        EditSupportUrl = game.SupportUrl ?? string.Empty;
        EditReleaseStatusIndex = GameStoreLabels.ReleaseStatusIndex(game.ReleaseStatus);
        EditReleaseDate = game.ReleaseDateUtc;
        EditAgeRatingIndex = GameStoreLabels.AgeRatingIndex(game.AgeRating);
        EditLanguages = string.Join(", ", game.Languages);
        EditTags = string.Join(", ", game.Tags);
        HasSinglePlayer = game.Features.HasFlag(GameFeatures.SinglePlayer);
        HasMultiPlayer = game.Features.HasFlag(GameFeatures.MultiPlayer);
        HasCoOp = game.Features.HasFlag(GameFeatures.CoOp);
        HasController = game.Features.HasFlag(GameFeatures.ControllerSupport);
        HasCloudSaves = game.Features.HasFlag(GameFeatures.CloudSaves);
        HasAchievements = game.Features.HasFlag(GameFeatures.Achievements);
        HasModding = game.Features.HasFlag(GameFeatures.Modding);
        HasVr = game.Features.HasFlag(GameFeatures.Vr);
    }

    protected void ApplyMedia(GameDto game, string apiBaseUrl)
    {
        CoverImageUrl = CacheBust(InstallPathHelper.TryResolveDownloadUrl(
            apiBaseUrl,
            string.IsNullOrWhiteSpace(game.LogoUrl) ? game.ThumbnailUrl : game.LogoUrl));
        HeaderImageUrl = CacheBust(InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, game.LandscapeUrl));
        OwnCoverImageUrl = CacheBust(InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, game.CoverUrl));
        OwnHeaderImageUrl = CacheBust(InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, game.HeaderUrl));
        OwnLogoImageUrl = CacheBust(InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, game.LogoUrl));
    }

    private static string? CacheBust(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var sep = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{sep}v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    protected void ApplyCatalogLists(GameDto game, string apiBaseUrl)
    {
        ScreenshotItems.Clear();
        foreach (var shot in game.Screenshots ?? [])
        {
            ScreenshotItems.Add(new StudioScreenshotItem
            {
                Id = shot.Id,
                ImageUrl = InstallPathHelper.ResolveDownloadUrl(apiBaseUrl, shot.Url)
            });
        }

        Builds.Clear();
        foreach (var build in game.Builds)
        {
            Builds.Add(new StudioBuildItem
            {
                PlatformText = $"{build.Platform} {build.Architecture}".Trim(),
                Version = build.CurrentVersion,
                ExeName = build.ExeName,
                IsDefault = build.IsDefault
            });
        }
    }

    partial void OnDraftPChanged(bool value) => OnDraftOrFeatureChanged();
    partial void OnFeaturedPChanged(bool value) => OnDraftOrFeatureChanged();

    protected virtual void OnDraftOrFeatureChanged()
    {
    }

    protected void NotifyStorePresentation()
    {
        OnPropertyChanged(nameof(HasCover));
        OnPropertyChanged(nameof(HasHeaderArt));
        OnPropertyChanged(nameof(HasLogoArt));
        OnPropertyChanged(nameof(HasIntro));
        OnPropertyChanged(nameof(IsPublished));
        OnPropertyChanged(nameof(DraftLabel));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(TileLabel));
    }

    protected static string FormatSize(long bytes)
    {
        if (bytes <= 0)
        {
            return "—";
        }

        var mb = bytes / (1024d * 1024d);
        return mb >= 1024 ? $"{mb / 1024d:0.0} GB" : $"{mb:0.0} MB";
    }
}
