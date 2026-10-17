using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>One comment row as the comments control shows it.</summary>
public sealed class CreativeCommentItem(CreativeCommentDto dto, string apiBaseUrl)
{
    public CreativeCommentDto Dto { get; } = dto;
    public long Id => Dto.Id;
    public string Author => Dto.AuthorDisplayName;
    public string Text => Dto.Text;
    public bool CanDelete => Dto.CanDelete;
    public string AuthorInitial => string.IsNullOrWhiteSpace(Author) ? "?" : Author.Trim()[..1].ToUpperInvariant();

    public string? AvatarUrl { get; } = string.IsNullOrWhiteSpace(dto.AuthorAvatarUrl)
        ? null
        : Uri.TryCreate(dto.AuthorAvatarUrl, UriKind.Absolute, out _) || apiBaseUrl.Length == 0
            ? dto.AuthorAvatarUrl
            : $"{apiBaseUrl.TrimEnd('/')}/{dto.AuthorAvatarUrl.TrimStart('/')}";

    public bool HasAvatar => AvatarUrl is not null;
    public bool HasNoAvatar => AvatarUrl is null;

    public string DateText
    {
        get
        {
            var age = DateTimeOffset.UtcNow - Dto.CreatedAtUtc;
            if (age < TimeSpan.FromMinutes(1))
            {
                return Loc.T("przed chwilą");
            }

            if (age < TimeSpan.FromHours(1))
            {
                return Loc.F("{0} min temu", (int)age.TotalMinutes);
            }

            if (age < TimeSpan.FromDays(1))
            {
                return Loc.F("{0} godz. temu", (int)age.TotalHours);
            }

            return Dto.CreatedAtUtc.LocalDateTime.ToString("d MMM yyyy", Loc.Culture);
        }
    }
}

/// <summary>Comments of one work (art, music track or story); reused by all three hub sections.</summary>
public sealed partial class CreativeCommentsViewModel(ISgsApiService? api, string section, string apiBaseUrl = "")
    : ObservableObject
{
    public const int MaxLength = 2000;

    private int? _itemId;

    public ObservableCollection<CreativeCommentItem> Comments { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PostCommand))]
    public partial string NewCommentText { get; set; } = string.Empty;

    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PostCommand))]
    public partial bool IsBusy { get; set; }

    public bool HasComments => Comments.Count > 0;
    public bool HasNoComments => Comments.Count == 0;
    public bool HasStatus => !string.IsNullOrEmpty(StatusText);
    public string CommentCount => Loc.F("Komentarze ({0})", Comments.Count);

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    private void RaiseCounts()
    {
        OnPropertyChanged(nameof(HasComments));
        OnPropertyChanged(nameof(HasNoComments));
        OnPropertyChanged(nameof(CommentCount));
    }

    /// <summary>Clears the list and fetches the comments of <paramref name="itemId"/>.</summary>
    public async Task LoadAsync(int itemId)
    {
        _itemId = itemId;
        Comments.Clear();
        NewCommentText = string.Empty;
        StatusText = string.Empty;
        RaiseCounts();
        if (api is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var list = await api.GetCreativeCommentsAsync(section, itemId);
            if (_itemId != itemId)
            {
                return; // another work was opened meanwhile
            }

            foreach (var dto in list.OrderBy(c => c.CreatedAtUtc))
            {
                Comments.Add(new CreativeCommentItem(dto, apiBaseUrl));
            }
        }
        catch (Exception)
        {
            if (_itemId == itemId)
            {
                StatusText = Loc.T("Nie udało się wczytać komentarzy.");
            }
        }
        finally
        {
            if (_itemId == itemId)
            {
                IsBusy = false;
                RaiseCounts();
            }
        }
    }

    /// <summary>Forgets the current work (reader/detail closed).</summary>
    public void Clear()
    {
        _itemId = null;
        Comments.Clear();
        NewCommentText = string.Empty;
        StatusText = string.Empty;
        IsBusy = false;
        RaiseCounts();
    }

    private bool CanPost() => api is not null && _itemId is not null && !IsBusy && !string.IsNullOrWhiteSpace(NewCommentText);

    [RelayCommand(CanExecute = nameof(CanPost))]
    private async Task PostAsync()
    {
        if (_itemId is not { } itemId || api is null)
        {
            return;
        }

        var text = NewCommentText.Trim();
        if (text.Length > MaxLength)
        {
            StatusText = Loc.F("Komentarz może mieć maksymalnie {0} znaków.", MaxLength);
            return;
        }

        IsBusy = true;
        StatusText = string.Empty;
        try
        {
            var (item, errors) = await api.AddCreativeCommentAsync(section, itemId, text);
            if (_itemId != itemId)
            {
                return;
            }

            if (item is null)
            {
                StatusText = errors.Count > 0 ? Loc.TS(errors[0]) : Loc.T("Nie udało się dodać komentarza.");
                return;
            }

            Comments.Add(new CreativeCommentItem(item, apiBaseUrl));
            NewCommentText = string.Empty;
        }
        catch (Exception)
        {
            StatusText = Loc.T("Nie udało się dodać komentarza.");
        }
        finally
        {
            IsBusy = false;
            RaiseCounts();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(CreativeCommentItem? comment)
    {
        if (comment is null || api is null || _itemId is not { } itemId)
        {
            return;
        }

        IsBusy = true;
        StatusText = string.Empty;
        try
        {
            if (await api.DeleteCreativeCommentAsync(section, itemId, comment.Id))
            {
                Comments.Remove(comment);
            }
            else
            {
                StatusText = Loc.T("Nie udało się usunąć komentarza.");
            }
        }
        catch (Exception)
        {
            StatusText = Loc.T("Nie udało się usunąć komentarza.");
        }
        finally
        {
            IsBusy = false;
            RaiseCounts();
        }
    }
}
