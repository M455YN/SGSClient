using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.Core.Enums;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>Full-page profile of another player (see <see cref="UserProfileViewModel"/>).</summary>
public partial class UserProfileView : UserControl
{
    private UserProfileViewModel? _watched;

    public UserProfileView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_watched is not null)
            {
                _watched.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _watched = DataContext as UserProfileViewModel;
            if (_watched is null)
            {
                return;
            }

            _watched.SetShowChatHandler(ShowChatAsync);
            _watched.PropertyChanged += OnViewModelPropertyChanged;
            RefreshRemoveLabel();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UserProfileViewModel.Person))
        {
            RefreshRemoveLabel();
        }
    }

    private void RefreshRemoveLabel() =>
        RemoveFriendItem.Header = _watched?.Person?.FriendRelation == FriendRelation.OutgoingRequest
            ? Loc.T("Anuluj zaproszenie")
            : Loc.T("Usuń ze znajomych");

    private async Task ShowChatAsync(ChatModalViewModel chat)
    {
        var dialog = new FluentAvalonia.UI.Controls.FAContentDialog
        {
            Content = new ChatModal { DataContext = chat },
            CloseButtonText = Loc.T("Zamknij"),
            DefaultButton = FluentAvalonia.UI.Controls.FAContentDialogButton.Close
        };
        await dialog.ShowAsync(TopLevel.GetTopLevel(this));
    }

    private void OpenGame_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: RecentGameItem { GameId: > 0 } item }
            && TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel main)
        {
            main.OpenGameDetails(item.GameId, "Games");
        }
    }

    private void RemoveFriend_OnClick(object? sender, RoutedEventArgs e) =>
        _watched?.RemoveFriendCommand.Execute(null);

    private void OpenAuthor_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ProfileCommentItem comment }
            && TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel main)
        {
            main.OpenUserProfile(comment.AuthorUserId);
        }
    }

    private async void DeleteComment_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_watched is not null && sender is Control { Tag: ProfileCommentItem comment })
        {
            await _watched.DeleteCommentCommand.ExecuteAsync(comment);
        }
    }
}
