using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;

namespace SGSClient.UI.ViewModels;

public partial class AdminGameItem : GameStoreEditItem
{
    public int? ParentGameId { get; init; }
    public string? ParentSymbol { get; init; }
    public string? OwnerDisplayName { get; init; }
    public string? OwnerEmail { get; init; }
    public ObservableCollection<AdminGameItem> Dlcs { get; } = [];

    [ObservableProperty] public partial int? FeaturedOrder { get; set; }
    [ObservableProperty] public partial string PositionLabel { get; set; } = string.Empty;

    public bool HasDlcs => Dlcs.Count > 0;
    public string StateLabel => DraftP ? "Szkic" : "Opublikowana";
    public string OwnerText => string.IsNullOrWhiteSpace(OwnerDisplayName)
        ? (string.IsNullOrWhiteSpace(OwnerEmail) ? "—" : OwnerEmail!)
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
            ParentSymbol = game.ParentSymbol,
            OwnerDisplayName = game.OwnerDisplayName,
            OwnerEmail = game.OwnerEmail
        };
        item.ApplyDto(game, apiBaseUrl);
        return item;
    }

    public void ApplyDto(GameDto game, string? apiBaseUrl = null)
    {
        ApplyStoreEdit(game);
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

    protected override void OnDraftOrFeatureChanged()
    {
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(MetaText));
    }
}
