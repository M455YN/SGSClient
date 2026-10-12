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

public partial class GameDetailsViewModel
{
    public bool ReviewsAvailable => true;

    public ObservableCollection<GameReviewItem> Reviews { get; } = [];

    public IReadOnlyList<string> ReviewSortOptions { get; } = [Loc.T("Najnowsze"), Loc.T("Najbardziej pomocne")];
    public IReadOnlyList<string> ReviewFilterOptions { get; } = [Loc.T("Wszystkie"), Loc.T("Pozytywne"), Loc.T("Negatywne")];

    partial void OnSelectedReviewSortChanged(string value) => _ = LoadReviewsAsync();
    partial void OnSelectedReviewFilterChanged(string value) => _ = LoadReviewsAsync();

    private async Task LoadReviewsAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        try
        {
            // Option order: sort = newest, helpful; filter = all, positive, negative.
            var sort = ReviewSortOptions.ToList().IndexOf(SelectedReviewSort) == 1 ? "helpful" : "recent";
            bool? recommended = ReviewFilterOptions.ToList().IndexOf(SelectedReviewFilter) switch
            {
                1 => true,
                2 => false,
                _ => null
            };

            var summary = await _api.GetGameReviewsAsync(GameId, sort, recommended);
            Reviews.Clear();
            if (summary is null)
            {
                ReviewsSummaryText = Loc.T("Brak recenzji.");
                CanWriteReview = false;
                HasMyReview = false;
                return;
            }

            CanWriteReview = summary.CanReview;
            HasMyReview = summary.MyReview is not null;
            ReviewsSummaryText = summary.TotalCount == 0
                ? Loc.T("Brak recenzji — bądź pierwszy.")
                : Loc.F("{0:0.#}% z {1} recenzji poleca tę grę", summary.RecommendedPercent, summary.TotalCount);

            if (summary.MyReview is not null)
            {
                ReviewRecommended = summary.MyReview.Recommended;
                ReviewDraftText = summary.MyReview.Text;
            }

            foreach (var r in summary.Reviews)
            {
                Reviews.Add(GameReviewItem.FromDto(r));
            }
        }
        catch (Exception ex)
        {
            ReviewStatusText = ex.Message;
        }
    }

    [RelayCommand]
    private async Task VoteHelpfulAsync(GameReviewItem? item)
    {
        if (item is null || _api is null || item.IsMine)
        {
            return;
        }

        if (!await _api.VoteGameReviewAsync(item.Id, helpful: true))
        {
            ReviewStatusText = Loc.T("Nie udało się zagłosować.");
            return;
        }

        await LoadReviewsAsync();
    }

    [RelayCommand]
    private async Task ToggleCommentsAsync(GameReviewItem? item)
    {
        if (item is null || _api is null)
        {
            return;
        }

        if (item.CommentsExpanded)
        {
            item.CommentsExpanded = false;
            return;
        }

        item.CommentsExpanded = true;
        var comments = await _api.GetGameReviewCommentsAsync(item.Id);
        item.Comments.Clear();
        foreach (var c in comments)
        {
            item.Comments.Add(GameReviewCommentItem.FromDto(c));
        }
    }

    [RelayCommand]
    private async Task AddCommentAsync(GameReviewItem? item)
    {
        if (item is null || _api is null || string.IsNullOrWhiteSpace(item.CommentDraft))
        {
            return;
        }

        var (comment, error) = await _api.AddGameReviewCommentAsync(item.Id, item.CommentDraft);
        if (error is not null || comment is null)
        {
            ReviewStatusText = error ?? Loc.T("Nie udało się dodać komentarza.");
            return;
        }

        item.CommentDraft = string.Empty;
        item.CommentCount++;
        item.Comments.Add(GameReviewCommentItem.FromDto(comment));
        item.CommentsExpanded = true;
    }

    [RelayCommand]
    private async Task DeleteCommentAsync(GameReviewCommentItem? comment)
    {
        if (comment is null || _api is null)
        {
            return;
        }

        if (!await _api.DeleteGameReviewCommentAsync(comment.Id))
        {
            ReviewStatusText = Loc.T("Nie udało się usunąć komentarza.");
            return;
        }

        var parent = Reviews.FirstOrDefault(r => r.Id == comment.ReviewId);
        if (parent is not null)
        {
            parent.Comments.Remove(comment);
            parent.CommentCount = Math.Max(0, parent.CommentCount - 1);
        }
    }

    [RelayCommand]
    private async Task SubmitReviewAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        ReviewStatusText = Loc.T("Zapisywanie…");
        var (review, error) = await _api.UpsertGameReviewAsync(
            GameId,
            new UpsertGameReviewRequest
            {
                Recommended = ReviewRecommended,
                Text = ReviewDraftText
            });
        if (error is not null || review is null)
        {
            ReviewStatusText = error ?? Loc.T("Nie udało się zapisać recenzji.");
            return;
        }

        ReviewStatusText = Loc.T("Zapisano recenzję.");
        await LoadReviewsAsync();
    }

    [RelayCommand]
    private async Task DeleteReviewAsync()
    {
        if (_api is null || GameId <= 0)
        {
            return;
        }

        if (!await _api.DeleteGameReviewAsync(GameId))
        {
            ReviewStatusText = Loc.T("Nie udało się usunąć recenzji.");
            return;
        }

        ReviewDraftText = string.Empty;
        ReviewRecommended = true;
        ReviewStatusText = Loc.T("Usunięto recenzję.");
        await LoadReviewsAsync();
    }
}
