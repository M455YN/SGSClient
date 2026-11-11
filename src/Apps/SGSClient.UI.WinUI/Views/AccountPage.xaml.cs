using SGSClient.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using WinRT.Interop;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class AccountPage : Page, ILoadingPage
{
    private string _shownAvatarUrl = string.Empty;
    private string _shownBannerUrl = string.Empty;

    public AccountViewModel ViewModel { get; }

    public AccountPage()
    {
        ViewModel = App.Services.GetRequiredService<AccountViewModel>();
        InitializeComponent();
        CenteredColumn.Attach(this, ContentColumn, 1280);

        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        ViewModel.RecentGames.CollectionChanged += (_, _) => RefreshLists();
        ViewModel.Achievements.CollectionChanged += (_, _) => RefreshLists();
        ViewModel.FriendsPreview.CollectionChanged += (_, _) => RefreshLists();
        ViewModel.WallComments.CollectionChanged += (_, _) => RefreshLists();
        ViewModel.AchievementsRequested += async (gameId, title) =>
        {
            var modal = await ViewModel.CreateAchievementsModalAsync(gameId, title);
            var dialog = new ContentDialog
            {
                Content = new AchievementsModal { DataContext = modal },
                CloseButtonText = Loc.T("Zamknij"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            dialog.Resources["ContentDialogMaxWidth"] = 900d;
            dialog.Resources["ContentDialogMaxHeight"] = 800d;
            await dialog.ShowAsync();
        };
        ViewModel.SetLogoutHandler(App.ReturnToLoginGate);
        App.MainWindow?.AttachAccount(ViewModel);

        if (App.MainWindow is not null)
        {
            ViewModel.SetParentWindowHandleProvider(
                () => WindowNative.GetWindowHandle(App.MainWindow));
            ViewModel.SetNavigateHandler(tag =>
            {
                if (tag.StartsWith("Friends:", StringComparison.Ordinal))
                {
                    App.MainWindow.OpenSocialOverlay(tag[8..]);
                    return;
                }

                if (tag == "Friends")
                {
                    App.MainWindow.OpenSocialOverlay("Friends");
                }
            });
        }

    }

    public Task Ready { get; private set; } = Task.CompletedTask;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Ready = LoadForNavigationAsync();
    }

    private async Task LoadForNavigationAsync()
    {
        await ViewModel.InitializeAsync();
        RefreshUi();
        RefreshLists();
    }

    private void RefreshUi()
    {
        SignedOutPanel.Visibility = ToVisibility(ViewModel.ShowSignedOut);
        TagSetupPanel.Visibility = ToVisibility(ViewModel.ShowTagSetup);
        ProfileScroll.Visibility = ToVisibility(ViewModel.ShowAccountContent);
        SignedOutStatus.Text = ViewModel.AccountStatus;

        if (DraftTagBox.FocusState == FocusState.Unfocused)
        {
            DraftTagBox.Text = ViewModel.DraftPlayerTag;
        }

        TagPreviewText.Text = ViewModel.TagPreview;
        TagStatusText.Text = ViewModel.TagStatus;

        DisplayNameText.Text = ViewModel.DisplayName;
        PlayerTagText.Text = ViewModel.PlayerTagDisplay;
        PlayerTagText.Visibility = ToVisibility(!string.IsNullOrWhiteSpace(ViewModel.PlayerTag));
        BioText.Text = ViewModel.BioDisplay;
        LocationText.Text = ViewModel.Location;
        LocationPanel.Visibility = ToVisibility(!string.IsNullOrWhiteSpace(ViewModel.Location));
        MemberSinceText.Text = ViewModel.MemberSinceText;
        MemberSincePanel.Visibility = ToVisibility(!string.IsNullOrWhiteSpace(ViewModel.MemberSinceText));
        EmailText.Text = ViewModel.Email;

        AvatarPicture.DisplayName = ViewModel.DisplayName;
        AvatarPicture.Initials = ViewModel.AvatarInitials;
        EditAvatarPicture.DisplayName = ViewModel.DisplayName;
        EditAvatarPicture.Initials = ViewModel.AvatarInitials;
        if (_shownAvatarUrl != ViewModel.AvatarUrl)
        {
            _shownAvatarUrl = ViewModel.AvatarUrl;
            var avatar = ToBitmap(ViewModel.AvatarUrl);
            AvatarPicture.ProfilePicture = avatar;
            EditAvatarPicture.ProfilePicture = avatar;
        }

        if (_shownBannerUrl != ViewModel.BannerImageUrl)
        {
            _shownBannerUrl = ViewModel.BannerImageUrl;
            BannerImage.Source = ToBitmap(ViewModel.BannerImageUrl);
        }

        GamesCountText.Text = ViewModel.OwnedGamesCount.ToString(Loc.Culture);
        GamesPillText.Text = GamesCountText.Text;
        PlaytimeStatText.Text = string.IsNullOrEmpty(ViewModel.TotalPlaytimeText) ? "—" : ViewModel.TotalPlaytimeText;
        AchievementsStatText.Text = ViewModel.AchievementsUnlocked.ToString(Loc.Culture);
        AchievementsPillText.Text = AchievementsStatText.Text;
        AchievementsTotalText.Text = ViewModel.AchievementsTotalText;
        FriendsStatText.Text = ViewModel.FriendsCount.ToString(Loc.Culture);
        FriendsPillText.Text = FriendsStatText.Text;
        FollowersStatText.Text = ViewModel.FollowersCount.ToString(Loc.Culture);
        FollowingStatText.Text = ViewModel.FollowingCount.ToString(Loc.Culture);
        ActivitySummaryText.Text = ViewModel.TotalPlaytimeSummary;

        WallStatusText.Text = ViewModel.WallStatus;
        WallStatusText.Visibility = ToVisibility(!string.IsNullOrEmpty(ViewModel.WallStatus));
        if (WallCommentBox.FocusState == FocusState.Unfocused)
        {
            WallCommentBox.Text = ViewModel.WallCommentDraft;
        }

        AvatarActionStatusText.Text = ViewModel.ProfileActionStatus;
    }

    /// <summary>Re-binds the lists only when a collection changes, not on every property change.</summary>
    private void RefreshLists()
    {
        ActivityList.ItemsSource = ViewModel.RecentActivity;
        EmptyActivityPanel.Visibility = ToVisibility(ViewModel.ShowEmptyRecentGames);
        GamesGrid.ItemsSource = ViewModel.RecentGames;
        EmptyRecentText.Visibility = ToVisibility(ViewModel.ShowEmptyRecentGames);
        AchievementsList.ItemsSource = ViewModel.TopAchievements;
        FriendsPreviewList.ItemsSource = ViewModel.FriendsPreview;
        EmptyFriendsPanel.Visibility = ToVisibility(!ViewModel.HasFriendsPreview);
        WallCommentsList.ItemsSource = ViewModel.WallComments;
        CommentsCountText.Text = ViewModel.WallComments.Count.ToString(Loc.Culture);
    }

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static BitmapImage? ToBitmap(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? new BitmapImage { CreateOptions = BitmapCreateOptions.IgnoreImageCache, UriSource = uri }
            : null;

    private void WallCommentBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ViewModel.WallCommentDraft = WallCommentBox.Text ?? string.Empty;

    private void OpenLibraryAchievements_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: RecentGameItem item })
        {
            ViewModel.OpenLibraryAchievementsCommand.Execute(item);
        }
    }

    private void OpenGameAchievements_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AchievementSummaryItem item })
        {
            ViewModel.OpenGameAchievementsCommand.Execute(item);
        }
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
            App.MainWindow?.NavigateTo(typeof(GameDetailsPage), new GameDetailsNavArgs(item.GameId, "Library"), "Library");
        }
    }

    private void OpenFriendProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SocialPersonItem person })
        {
            App.MainWindow?.OpenUserProfile(person.UserId);
        }
    }

    private void OpenAuthor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProfileCommentItem comment })
        {
            App.MainWindow?.OpenUserProfile(comment.AuthorUserId);
        }
    }

    private void OpenLibrary_Click(object sender, RoutedEventArgs e) => App.MainWindow?.NavigateByTagPublic("Library");

    private void OpenStore_Click(object sender, RoutedEventArgs e) => App.MainWindow?.NavigateByTagPublic("Games");

    private void FindFriends_Click(object sender, RoutedEventArgs e) => ViewModel.OpenFriendsCommand.Execute("Search");

    private void CopyTag_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.PlayerTagDisplay))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(ViewModel.PlayerTagDisplay);
        Clipboard.SetContent(package);
        CopiedTip.Subtitle = ViewModel.PlayerTagDisplay;
        CopiedTip.IsOpen = true;
    }

    private async void PostWall_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.WallCommentDraft = WallCommentBox.Text ?? string.Empty;
        await ViewModel.PostWallCommentCommand.ExecuteAsync(null);
        WallCommentBox.Text = ViewModel.WallCommentDraft;
        RefreshUi();
    }

    private async void DeleteWall_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ProfileCommentItem comment)
        {
            await ViewModel.DeleteWallCommentCommand.ExecuteAsync(comment);
            RefreshUi();
        }
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoginCommand.ExecuteAsync(null);
        RefreshUi();
        App.MainWindow?.RefreshSidebar();
    }

    private async void SignOut_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LogoutCommand.ExecuteAsync(null);
        RefreshUi();
        App.MainWindow?.RefreshSidebar();
    }

    private void OpenFriends_Click(object sender, RoutedEventArgs e)
    {
        var tab = (sender as FrameworkElement)?.Tag as string ?? "Friends";
        ViewModel.OpenFriendsCommand.Execute(tab);
    }

    private async void ClaimTag_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DraftPlayerTag = DraftTagBox.Text ?? string.Empty;
        await ViewModel.ClaimPlayerTagCommand.ExecuteAsync(null);
        RefreshUi();
        App.MainWindow?.RefreshSidebar();
    }

    private void DraftTag_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.DraftPlayerTag = DraftTagBox.Text ?? string.Empty;
        TagPreviewText.Text = ViewModel.TagPreview;
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BeginEditCommand.Execute(null);
        EditDisplayNameBox.Text = ViewModel.EditDisplayName;
        EditBioBox.Text = ViewModel.EditBio;
        EditLocationBox.Text = ViewModel.EditLocation;
        EditHideAchievementsBox.IsOn = ViewModel.EditHideAchievements;
        EditHideGameActivityBox.IsOn = ViewModel.EditHideGameActivity;
        EditPlayerTagBox.Text = ViewModel.EditPlayerTag;
        EditErrorBar.IsOpen = false;
        RefreshUi();
        EditDialog.XamlRoot = XamlRoot;
        await EditDialog.ShowAsync();
    }

    private async void EditDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            ViewModel.EditDisplayName = EditDisplayNameBox.Text ?? string.Empty;
            ViewModel.EditBio = EditBioBox.Text ?? string.Empty;
            ViewModel.EditLocation = EditLocationBox.Text ?? string.Empty;
            ViewModel.EditHideAchievements = EditHideAchievementsBox.IsOn;
            ViewModel.EditHideGameActivity = EditHideGameActivityBox.IsOn;
            ViewModel.EditPlayerTag = EditPlayerTagBox.Text ?? string.Empty;
            await ViewModel.SaveProfileCommand.ExecuteAsync(null);

            // A failed save leaves the editor open with the reason in AccountStatus.
            if (ViewModel.IsEditing)
            {
                args.Cancel = true;
                EditErrorBar.Message = ViewModel.AccountStatus;
                EditErrorBar.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            EditErrorBar.Message = ex.Message;
            EditErrorBar.IsOpen = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void EditDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        if (ViewModel.IsEditing)
        {
            ViewModel.CancelEditCommand.Execute(null);
        }

        RefreshUi();
    }

    private async void ChangeAvatar_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var path = await WinUiFilePicker.PickSingleFileAsync(
            App.MainWindow,
            [".png", ".jpg", ".jpeg", ".webp", ".gif"],
            commitLabel: Loc.T("Ustaw awatar"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await using var stream = File.OpenRead(path);
        await ViewModel.UploadAvatarFromStreamAsync(stream, Path.GetFileName(path));
        RefreshUi();
    }

    private async void ClearAvatar_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearAvatarCommand.ExecuteAsync(null);
        RefreshUi();
    }
}
