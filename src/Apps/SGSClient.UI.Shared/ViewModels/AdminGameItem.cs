using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;

namespace SGSClient.UI.ViewModels;

public partial class AdminGameItem : GameStoreEditItem
{
    public int? ParentGameId { get; init; }
    public string? ParentSymbol { get; init; }
    public ObservableCollection<AdminGameItem> Dlcs { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OwnerText))]
    public partial int OwnerUserId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OwnerText))]
    public partial string? OwnerDisplayName { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OwnerText))]
    public partial string? OwnerEmail { get; set; }

    [ObservableProperty] public partial int EditOwnerUserId { get; set; }
    [ObservableProperty] public partial int? FeaturedOrder { get; set; }
    [ObservableProperty] public partial string PositionLabel { get; set; } = string.Empty;

    public bool HasDlcs => Dlcs.Count > 0;
    public string StateLabel => DraftP ? "Szkic" : "Opublikowana";
    public string OwnerText => string.IsNullOrWhiteSpace(OwnerDisplayName)
        ? (string.IsNullOrWhiteSpace(OwnerEmail) ? $"#{OwnerUserId}" : OwnerEmail!)
        : OwnerDisplayName!;
    public string MetaText => $"{Symbol} · {KindLabel} · {StateLabel}";

    public static AdminGameItem FromDto(GameDto game, string apiBaseUrl)
    {
        var item = new AdminGameItem
        {
            Id = game.Id,
            Title = game.Title,
            Symbol = game.Symbol,
            IsDlc = game.Kind == GameContentKind.Dlc,
            ParentGameId = game.ParentGameId,
            ParentSymbol = game.ParentSymbol
        };
        item.ApplyDto(game, apiBaseUrl);
        return item;
    }

    public void ApplyDto(GameDto game, string? apiBaseUrl = null)
    {
        ApplyStoreEdit(game);
        OwnerUserId = game.UserId;
        EditOwnerUserId = game.UserId;
        OwnerDisplayName = game.OwnerDisplayName;
        OwnerEmail = game.OwnerEmail;
        FeaturedOrder = game.FeaturedOrder;
        if (apiBaseUrl is not null)
        {
            ApplyMedia(game, apiBaseUrl);
            ApplyCatalogLists(game, apiBaseUrl);
        }

        NotifyStorePresentation();
        OnPropertyChanged(nameof(HasDlcs));
        OnPropertyChanged(nameof(OwnerText));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(MetaText));
    }

    public new UpdateGameRequestDto ToUpdateRequest()
    {
        var request = base.ToUpdateRequest();
        return new UpdateGameRequestDto
        {
            Title = request.Title,
            Description = request.Description,
            HardwareRequirements = request.HardwareRequirements,
            OtherInformation = request.OtherInformation,
            Genres = request.Genres,
            Engines = request.Engines,
            DraftP = request.DraftP,
            FeaturedP = request.FeaturedP,
            FeaturedOrder = request.FeaturedOrder,
            StudioName = request.StudioName,
            WebsiteUrl = request.WebsiteUrl,
            DiscordUrl = request.DiscordUrl,
            SupportUrl = request.SupportUrl,
            ReleaseStatus = request.ReleaseStatus,
            ReleaseDateUtc = request.ReleaseDateUtc,
            ClearReleaseDate = request.ClearReleaseDate,
            AgeRating = request.AgeRating,
            Features = request.Features,
            Languages = request.Languages,
            Tags = request.Tags,
            OwnerUserId = EditOwnerUserId > 0 ? EditOwnerUserId : null
        };
    }

    protected override void OnDraftOrFeatureChanged()
    {
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(MetaText));
    }
}
