using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Core.Playtime;

namespace SGSClient.UI.ViewModels;

public partial class GameReviewItem : ObservableObject
{
    public long Id { get; init; }
    public required string AuthorDisplayName { get; init; }
    public string? AuthorPlayerTag { get; init; }
    public bool Recommended { get; init; }
    public required string Text { get; init; }
    public string VerdictLabel => Recommended ? Loc.T("Poleca") : Loc.T("Nie poleca");
    public string MetaText { get; init; } = string.Empty;
    public bool IsMine { get; init; }
    public int HelpfulCount { get; set; }
    public bool? MyHelpfulVote { get; set; }
    public int CommentCount { get; set; }
    public bool CanVote => !IsMine;
    public string HelpfulButtonText => MyHelpfulVote == true
        ? Loc.F("Pomocne ✓ ({0})", HelpfulCount)
        : $"Pomocne ({HelpfulCount})";
    public string CommentsToggleText => CommentsExpanded
        ? Loc.T("Ukryj komentarze")
        : $"Komentarze ({CommentCount})";

    public ObservableCollection<GameReviewCommentItem> Comments { get; } = [];

    [ObservableProperty] public partial bool CommentsExpanded { get; set; }
    [ObservableProperty] public partial string CommentDraft { get; set; } = string.Empty;

    partial void OnCommentsExpandedChanged(bool value) => OnPropertyChanged(nameof(CommentsToggleText));

    public static GameReviewItem FromDto(GameReviewDto dto)
    {
        var play = dto.PlaytimeSecondsAtReview is > 0
            ? $" · {PlaytimeFormat.ToPolishTotal(dto.PlaytimeSecondsAtReview.Value)}"
            : string.Empty;
        var tag = string.IsNullOrWhiteSpace(dto.AuthorPlayerTag) ? "" : $" @{dto.AuthorPlayerTag}";
        return new GameReviewItem
        {
            Id = dto.Id,
            AuthorDisplayName = dto.AuthorDisplayName,
            AuthorPlayerTag = dto.AuthorPlayerTag,
            Recommended = dto.Recommended,
            Text = dto.Text,
            IsMine = dto.IsMine,
            HelpfulCount = dto.HelpfulCount,
            MyHelpfulVote = dto.MyHelpfulVote,
            CommentCount = dto.CommentCount,
            MetaText = $"{(string.IsNullOrWhiteSpace(dto.AuthorDisplayName) ? "Gracz" : dto.AuthorDisplayName)}{tag} · {dto.UpdatedAtUtc.LocalDateTime:g}{play}"
        };
    }
}

public sealed class GameReviewCommentItem
{
    public long Id { get; init; }
    public long ReviewId { get; init; }
    public required string AuthorDisplayName { get; init; }
    public required string Text { get; init; }
    public string MetaText { get; init; } = string.Empty;
    public bool IsMine { get; init; }

    public static GameReviewCommentItem FromDto(GameReviewCommentDto dto)
    {
        var tag = string.IsNullOrWhiteSpace(dto.AuthorPlayerTag) ? "" : $" @{dto.AuthorPlayerTag}";
        return new GameReviewCommentItem
        {
            Id = dto.Id,
            ReviewId = dto.ReviewId,
            AuthorDisplayName = dto.AuthorDisplayName,
            Text = dto.Text,
            IsMine = dto.IsMine,
            MetaText = $"{(string.IsNullOrWhiteSpace(dto.AuthorDisplayName) ? "Gracz" : dto.AuthorDisplayName)}{tag} · {dto.CreatedAtUtc.LocalDateTime:g}"
        };
    }
}
