using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SGSClient.UI.WinUI.ViewModels;
using Windows.Storage.Pickers;
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

        if (App.MainWindow is not null)
        {
            ViewModel.SetParentWindowHandleProvider(
                () => WindowNative.GetWindowHandle(App.MainWindow));
            ViewModel.SetNavigateHandler(tag =>
            {
                if (tag.StartsWith("Friends:", StringComparison.Ordinal))
                {
                    App.MainWindow.NavigateTo(typeof(FriendsPage), parameter: tag[8..], selectTag: "Friends");
                    return;
                }

                if (tag == "Friends")
                {
                    App.MainWindow.NavigateTo(typeof(FriendsPage), parameter: "Friends", selectTag: "Friends");
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
        FriendsText.Content = $"{ViewModel.FriendsCount} Znajomi";
        FollowingText.Content = $"{ViewModel.FollowingCount} Obserwowane";
        FollowersText.Content = $"{ViewModel.FollowersCount} Obserwujący";
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
        ViewModel.EditPlayerTag = EditPlayerTagBox.Text ?? string.Empty;
        await ViewModel.SaveProfileCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void ChangeAvatar_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        if (App.MainWindow is not null)
        {
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        }

        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".gif");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        // WinUI often returns empty Path for picked files — stream the content instead.
        await using var stream = await file.OpenStreamForReadAsync();
        await ViewModel.UploadAvatarFromStreamAsync(stream, file.Name);
        RefreshUi();
    }

    private async void ClearAvatar_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearAvatarCommand.ExecuteAsync(null);
        RefreshUi();
    }
}
