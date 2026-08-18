using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class FriendsPage : Page
{
    public FriendsViewModel ViewModel { get; }

    public FriendsPage()
    {
        ViewModel = App.Services.GetRequiredService<FriendsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var tab = e.Parameter as string;
        await ViewModel.InitializeAsync(tab);
        RefreshUi();
    }

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;
        SearchBox.Text = ViewModel.SearchQuery;
        TabRequests.Content = ViewModel.RequestsTabLabel;

        HighlightTab(TabFriends, ViewModel.ShowFriendsTab);
        HighlightTab(TabRequests, ViewModel.ShowRequestsTab);
        HighlightTab(TabFollowing, ViewModel.ShowFollowingTab);
        HighlightTab(TabFollowers, ViewModel.ShowFollowersTab);
        HighlightTab(TabSearch, ViewModel.ShowSearchTab);

        FriendsPanel.Visibility = ViewModel.ShowFriendsTab ? Visibility.Visible : Visibility.Collapsed;
        RequestsPanel.Visibility = ViewModel.ShowRequestsTab ? Visibility.Visible : Visibility.Collapsed;
        FollowingPanel.Visibility = ViewModel.ShowFollowingTab ? Visibility.Visible : Visibility.Collapsed;
        FollowersPanel.Visibility = ViewModel.ShowFollowersTab ? Visibility.Visible : Visibility.Collapsed;
        SearchPanel.Visibility = ViewModel.ShowSearchTab ? Visibility.Visible : Visibility.Collapsed;

        EmptyFriendsText.Visibility = ViewModel.ShowEmptyFriends ? Visibility.Visible : Visibility.Collapsed;
        EmptyIncomingText.Visibility = ViewModel.HasIncoming ? Visibility.Collapsed : Visibility.Visible;
        EmptyOutgoingText.Visibility = ViewModel.HasOutgoing ? Visibility.Collapsed : Visibility.Visible;
        EmptyFollowingText.Visibility = ViewModel.ShowEmptyFollowing ? Visibility.Visible : Visibility.Collapsed;
        EmptyFollowersText.Visibility = ViewModel.ShowEmptyFollowers ? Visibility.Visible : Visibility.Collapsed;
        EmptySearchText.Visibility = ViewModel.ShowEmptySearch ? Visibility.Visible : Visibility.Collapsed;

        FriendsList.ItemsSource = ViewModel.Friends;
        IncomingList.ItemsSource = ViewModel.Incoming;
        OutgoingList.ItemsSource = ViewModel.Outgoing;
        FollowingList.ItemsSource = ViewModel.Following;
        FollowersList.ItemsSource = ViewModel.Followers;
        SearchList.ItemsSource = ViewModel.SearchResults;
    }

    private static void HighlightTab(Button button, bool selected) =>
        button.FontWeight = selected ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string tab)
        {
            ViewModel.SelectTabCommand.Execute(tab);
            RefreshUi();
        }
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SearchQuery = SearchBox.Text ?? string.Empty;
        await ViewModel.SearchCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void AddFriend_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SocialPersonItem person)
        {
            await ViewModel.AddFriendCommand.ExecuteAsync(person);
            RefreshUi();
        }
    }

    private async void Accept_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SocialPersonItem person)
        {
            await ViewModel.AcceptFriendCommand.ExecuteAsync(person);
            RefreshUi();
        }
    }

    private async void RemoveFriend_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SocialPersonItem person)
        {
            await ViewModel.RemoveFriendCommand.ExecuteAsync(person);
            RefreshUi();
        }
    }

    private async void Follow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SocialPersonItem person)
        {
            await ViewModel.FollowCommand.ExecuteAsync(person);
            RefreshUi();
        }
    }

    private async void Unfollow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SocialPersonItem person)
        {
            await ViewModel.UnfollowCommand.ExecuteAsync(person);
            RefreshUi();
        }
    }
}
