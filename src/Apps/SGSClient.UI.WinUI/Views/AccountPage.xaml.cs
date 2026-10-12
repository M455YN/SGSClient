using SGSClient.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SGSClient.UI.ViewModels;
using WinRT.Interop;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class AccountPage : Page
{
    public AccountViewModel ViewModel { get; }

    public AccountPage()
    {
        ViewModel = App.Services.GetRequiredService<AccountViewModel>();
        InitializeComponent();

        ViewModel.PropertyChanged += (_, _) => RefreshUi();
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

        Loaded += async (_, _) =>
        {
            await ViewModel.InitializeAsync();
            RefreshUi();
        };
    }

    private void RefreshUi()
    {
        SignedOutPanel.Visibility = ViewModel.ShowSignedOut ? Visibility.Visible : Visibility.Collapsed;
        TagSetupPanel.Visibility = ViewModel.ShowTagSetup ? Visibility.Visible : Visibility.Collapsed;
        ProfileScroll.Visibility = ViewModel.IsLoggedIn && !ViewModel.NeedsPlayerTagSetup
            ? Visibility.Visible
            : Visibility.Collapsed;
        SignedOutStatus.Text = ViewModel.AccountStatus;

        DisplayNameText.Text = ViewModel.DisplayName;
        PlayerTagText.Text = ViewModel.PlayerTagDisplay;
        PlayerTagText.Visibility = string.IsNullOrWhiteSpace(ViewModel.PlayerTag)
            ? Visibility.Collapsed
            : Visibility.Visible;
        LastActivityText.Text = ViewModel.LastActivityText;
        if (DraftTagBox.FocusState == FocusState.Unfocused)
        {
            DraftTagBox.Text = ViewModel.DraftPlayerTag;
        }

        TagPreviewText.Text = ViewModel.TagPreview;
        TagStatusText.Text = ViewModel.TagStatus;
        FriendsText.Content = Loc.F("{0} Znajomi", ViewModel.FriendsCount);
        FollowingText.Content = $"{ViewModel.FollowingCount} Obserwowane";
        FollowersText.Content = Loc.F("{0} Obserwujący", ViewModel.FollowersCount);
        BioText.Text = ViewModel.BioDisplay;
        LocationText.Text = ViewModel.LocationDisplay;
        EmailText.Text = ViewModel.Email;
        AvatarInitialsText.Text = ViewModel.AvatarInitials;

        RightRail.Visibility = ViewModel.ShowProfile ? Visibility.Visible : Visibility.Collapsed;
        EditPanel.Visibility = ViewModel.IsEditing ? Visibility.Visible : Visibility.Collapsed;

        if (ViewModel.IsEditing)
        {
            EditDisplayNameBox.Text = ViewModel.EditDisplayName;
            EditBioBox.Text = ViewModel.EditBio;
            EditLocationBox.Text = ViewModel.EditLocation;
            EditHideAchievementsBox.IsChecked = ViewModel.EditHideAchievements;
            EditPlayerTagBox.Text = ViewModel.EditPlayerTag;
        }

        AvatarActionStatusText.Text = ViewModel.ProfileActionStatus;

        if (ViewModel.HasAvatar && Uri.TryCreate(ViewModel.AvatarUrl, UriKind.Absolute, out var uri))
        {
            var image = new BitmapImage();
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = uri;
            AvatarBrush.ImageSource = image;
            AvatarInitialsText.Visibility = Visibility.Collapsed;
        }
        else
        {
            AvatarBrush.ImageSource = null;
            AvatarInitialsText.Visibility = Visibility.Visible;
        }

        LibraryList.ItemsSource = ViewModel.RecentGames;
        RecentGrid.ItemsSource = ViewModel.RecentGames;
        EmptyRecentText.Visibility = ViewModel.ShowEmptyRecentGames ? Visibility.Visible : Visibility.Collapsed;
        AchievementsList.ItemsSource = ViewModel.Achievements;
        AchievementsTotalText.Text = ViewModel.AchievementsTotalText;

        WallStatusText.Text = ViewModel.WallStatus;
        WallCommentsList.ItemsSource = ViewModel.WallComments;
        if (WallCommentBox.FocusState == FocusState.Unfocused)
        {
            WallCommentBox.Text = ViewModel.WallCommentDraft;
        }
    }

    private void WallCommentBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ViewModel.WallCommentDraft = WallCommentBox.Text ?? string.Empty;

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

    private void OpenFriendsPage_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenFriendsCommand.Execute("Friends");

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

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BeginEditCommand.Execute(null);
        RefreshUi();
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelEditCommand.Execute(null);
        RefreshUi();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.EditDisplayName = EditDisplayNameBox.Text ?? string.Empty;
        ViewModel.EditBio = EditBioBox.Text ?? string.Empty;
        ViewModel.EditLocation = EditLocationBox.Text ?? string.Empty;
        ViewModel.EditHideAchievements = EditHideAchievementsBox.IsChecked == true;
        ViewModel.EditPlayerTag = EditPlayerTagBox.Text ?? string.Empty;
        await ViewModel.SaveProfileCommand.ExecuteAsync(null);
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
