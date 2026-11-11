using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.Core.Enums;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

/// <summary>Full-page profile of another player. Navigation parameter: the user id.</summary>
public sealed partial class UserProfilePage : Page, ILoadingPage
{
    public UserProfileViewModel ViewModel { get; }

    public UserProfilePage()
    {
        ViewModel = App.Services.GetRequiredService<UserProfileViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.SetShowChatHandler(ShowChatAsync);
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(UserProfileViewModel.AvatarUrl) or nameof(UserProfileViewModel.Person))
            {
                RefreshAvatarAndMenu();
            }

            if (e.PropertyName == nameof(UserProfileViewModel.BannerImageUrl))
            {
                BannerImage.Source = Uri.TryCreate(ViewModel.BannerImageUrl, UriKind.Absolute, out var banner)
                    ? new BitmapImage(banner)
                    : null;
            }
        };
    }

    public Task Ready { get; private set; } = Task.CompletedTask;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Ready = ViewModel.LoadAsync(e.Parameter is int userId ? userId : 0);
    }

    private void RefreshAvatarAndMenu()
    {
        AvatarPicture.ProfilePicture = Uri.TryCreate(ViewModel.AvatarUrl, UriKind.Absolute, out var uri)
            ? new BitmapImage(uri)
            : null;
        RemoveFriendItem.Text = ViewModel.Person?.FriendRelation == FriendRelation.OutgoingRequest
            ? Loc.T("Anuluj zaproszenie")
            : Loc.T("Usuń ze znajomych");
    }

    private async Task ShowChatAsync(ChatModalViewModel chat)
    {
        var dialog = new ContentDialog
        {
            Content = new ChatModal { DataContext = chat },
            CloseButtonText = Loc.T("Zamknij"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        dialog.Resources["ContentDialogMaxWidth"] = 560d;
        dialog.Resources["ContentDialogMaxHeight"] = 720d;
        await dialog.ShowAsync();
    }

    private void OpenGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: RecentGameItem item })
        {
            OpenGameDetails(item);
        }
    }

    private void GamesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecentGameItem item)
        {
            OpenGameDetails(item);
        }
    }

    private static void OpenGameDetails(RecentGameItem item)
    {
        if (item.GameId > 0)
        {
            App.MainWindow?.NavigateTo(typeof(GameDetailsPage), new GameDetailsNavArgs(item.GameId, "Games"), "Games");
        }
    }

    private void RemoveFriend_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveFriendCommand.Execute(null);

    private void OpenAuthor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProfileCommentItem { AuthorUserId: > 0 } comment })
        {
            App.MainWindow?.OpenUserProfile(comment.AuthorUserId);
        }
    }

    private async void DeleteComment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProfileCommentItem comment })
        {
            await ViewModel.DeleteCommentCommand.ExecuteAsync(comment);
        }
    }
}
