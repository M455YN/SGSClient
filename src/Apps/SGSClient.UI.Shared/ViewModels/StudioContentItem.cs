using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;

namespace SGSClient.UI.ViewModels;

public partial class StudioContentItem : GameStoreEditItem
{
    public string? ParentSymbol { get; init; }
    public ObservableCollection<StudioContentItem> Dlcs { get; } = [];

    [ObservableProperty] public partial string LanguagesText { get; set; } = string.Empty;
    [ObservableProperty] public partial string TagsText { get; set; } = string.Empty;
    [ObservableProperty] public partial string PackageSizeText { get; set; } = "—";
    [ObservableProperty] public partial string CreatedText { get; set; } = "—";

    public bool HasDlcs => Dlcs.Count > 0;
    public string ReleaseLabel => GameStoreLabels.ReleaseStatus(ReleaseStatus);
    public string AgeLabel => GameStoreLabels.AgeRating(AgeRating);
    public string MetaText => IsDlc
        ? $"{Symbol} · DLC · {DraftLabel}"
        : $"{Symbol} · {DraftLabel} · {Dlcs.Count} DLC · {ReleaseLabel}";

    public static StudioContentItem FromGame(GameDto game, string apiBaseUrl)
    {
        var item = new StudioContentItem
        {
            Id = game.Id,
            Title = game.Title,
            Symbol = game.Symbol,
            IsDlc = game.Kind == GameContentKind.Dlc,
            ParentSymbol = game.ParentSymbol
        };
        item.ApplyDto(game, apiBaseUrl);
        return item;
    }

    public static StudioContentItem FromDlc(GameDlcDto dlc, string parentSymbol, bool parentDraft, string apiBaseUrl) => new()
    {
        Id = dlc.Id,
        Title = dlc.Title,
        Symbol = dlc.Symbol,
        IsDlc = true,
        ParentSymbol = parentSymbol,
        CoverImageUrl = InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, dlc.CoverUrl),
        DraftP = parentDraft,
        EditTitle = dlc.Title,
        PackageSizeText = FormatSize(dlc.PackageSizeBytes),
        CurrentVersionText = dlc.PreferredVersion ?? "—"
    };

    public void ApplyDto(GameDto game, string apiBaseUrl)
    {
        ApplyStoreEdit(game);
        ApplyMedia(game, apiBaseUrl);
        ApplyCatalogLists(game, apiBaseUrl);
        LanguagesText = string.Join(", ", game.Languages);
        TagsText = string.Join(", ", game.Tags);
        PackageSizeText = FormatSize(game.PackageSizeBytes);
        CreatedText = game.CreatedAtUtc.LocalDateTime.ToString("d MMM yyyy", Loc.Culture);
        NotifyStorePresentation();
        OnPropertyChanged(nameof(HasDlcs));
        OnPropertyChanged(nameof(ReleaseLabel));
        OnPropertyChanged(nameof(AgeLabel));
        OnPropertyChanged(nameof(MetaText));
    }

    protected override void OnDraftOrFeatureChanged() => OnPropertyChanged(nameof(MetaText));
}
