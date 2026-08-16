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
        ProfileScroll.Visibility = ViewModel.IsLoggedIn ? Visibility.Visible : Visibility.Collapsed;
        SignedOutStatus.Text = ViewModel.AccountStatus;

        DisplayNameText.Text = ViewModel.DisplayName;
        LastActivityText.Text = ViewModel.LastActivityText;
        FriendsText.Text = $"{ViewModel.FriendsCount} Znajomi";
        FollowingText.Text = $"{ViewModel.FollowingCount} Obserwowane";
        FollowersText.Text = $"{ViewModel.FollowersCount} Obserwujący";
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
        }

        if (ViewModel.HasAvatar && Uri.TryCreate(ViewModel.AvatarUrl, UriKind.Absolute, out var uri))
        {
            AvatarBrush.ImageSource = new BitmapImage(uri);
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

        await ViewModel.UploadAvatarFromPathAsync(file.Path);
        RefreshUi();
    }

    private async void ClearAvatar_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearAvatarCommand.ExecuteAsync(null);
        RefreshUi();
    }
}
