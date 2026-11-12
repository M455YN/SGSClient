using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;
using Windows.System;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class FriendsPage : Page
{
    private bool _syncingTabs;

    public FriendsViewModel ViewModel { get; }

    public FriendsPage()
    {
        ViewModel = App.Services.GetRequiredService<FriendsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        ViewModel.SetShowChatHandler(ShowChatAsync);
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

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync(e.Parameter as string);
        RefreshUi();
    }

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;

        if (SearchBox.FocusState == FocusState.Unfocused)
        {
            SearchBox.Text = ViewModel.SearchQuery;
            SearchBox.PlaceholderText = ViewModel.SearchPlaceholder;
        }

        SearchBox.Visibility = ViewModel.ShowMainList ? Visibility.Visible : Visibility.Collapsed;

        _syncingTabs = true;
        SocialTabs.SelectedItem = ViewModel.ShowChatTab
            ? TabChat
            : ViewModel.ShowSearchTab
                ? TabSearch
                : TabFriends;
        _syncingTabs = false;

        ChatPanel.Visibility = ViewModel.ShowChatTab ? Visibility.Visible : Visibility.Collapsed;
        FriendsPanel.Visibility = ViewModel.ShowFriendsTab ? Visibility.Visible : Visibility.Collapsed;
        RequestsPanel.Visibility = ViewModel.ShowRequestsTab ? Visibility.Visible : Visibility.Collapsed;
        FollowingPanel.Visibility = ViewModel.ShowFollowingTab ? Visibility.Visible : Visibility.Collapsed;
        FollowersPanel.Visibility = ViewModel.ShowFollowersTab ? Visibility.Visible : Visibility.Collapsed;
        SearchPanel.Visibility = ViewModel.ShowSearchTab ? Visibility.Visible : Visibility.Collapsed;

        ChatStatusText.Text = ViewModel.ChatStatus;
        EmptyChatText.Visibility = ViewModel.ShowEmptyChat ? Visibility.Visible : Visibility.Collapsed;
        ChatThreadsList.ItemsSource = ViewModel.ChatThreads;

        EmptyFriendsText.Visibility = ViewModel.ShowEmptyFriends ? Visibility.Visible : Visibility.Collapsed;
        IncomingExpander.Visibility = ViewModel.ShowFriendsTab && ViewModel.HasIncoming
            ? Visibility.Visible
            : Visibility.Collapsed;
        IncomingBadge.Value = ViewModel.IncomingCount;

        OnlineExpander.Visibility = ViewModel.ShowFriendsGroups ? Visibility.Visible : Visibility.Collapsed;
        OfflineExpander.Visibility = ViewModel.ShowFriendsGroups ? Visibility.Visible : Visibility.Collapsed;
        OnlineBadge.Value = ViewModel.OnlineCount;
        OfflineBadge.Value = ViewModel.OfflineCount;

        EmptyIncomingText.Visibility = ViewModel.HasIncoming ? Visibility.Collapsed : Visibility.Visible;
        EmptyOutgoingText.Visibility = ViewModel.HasOutgoing ? Visibility.Collapsed : Visibility.Visible;
        EmptyFollowingText.Visibility = ViewModel.ShowEmptyFollowing ? Visibility.Visible : Visibility.Collapsed;
        EmptyFollowersText.Visibility = ViewModel.ShowEmptyFollowers ? Visibility.Visible : Visibility.Collapsed;
        EmptySearchText.Visibility = ViewModel.ShowEmptySearch ? Visibility.Visible : Visibility.Collapsed;

        IncomingList.ItemsSource = ViewModel.Incoming;
        IncomingListAlt.ItemsSource = ViewModel.Incoming;
        OnlineList.ItemsSource = ViewModel.OnlineFriends;
        OfflineList.ItemsSource = ViewModel.OfflineFriends;
        OutgoingList.ItemsSource = ViewModel.Outgoing;
        FollowingList.ItemsSource = ViewModel.Following;
        FollowersList.ItemsSource = ViewModel.Followers;
        SearchList.ItemsSource = ViewModel.SearchResults;
    }

    private void SocialTabs_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_syncingTabs || sender.SelectedItem is not SelectorBarItem { Tag: string tag })
        {
            return;
        }

        if (tag == "Profile")
        {
            App.MainWindow?.CloseSocialOverlay();
            App.MainWindow?.NavigateTo(typeof(AccountPage), selectTag: "Account");
            return;
        }

        ViewModel.SelectTabCommand.Execute(tag);
        RefreshUi();
    }

    private void Close_Click(object sender, RoutedEventArgs e) =>
        App.MainWindow?.CloseSocialOverlay();

    private void IncomingExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args) =>
        ViewModel.IncomingExpanded = true;

    private void IncomingExpander_Collapsed(Expander sender, ExpanderCollapsedEventArgs args) =>
        ViewModel.IncomingExpanded = false;

    private void OnlineExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args) =>
        ViewModel.OnlineExpanded = true;

    private void OnlineExpander_Collapsed(Expander sender, ExpanderCollapsedEventArgs args) =>
        ViewModel.OnlineExpanded = false;

    private void OfflineExpander_Expanding(Expander sender, ExpanderExpandingEventArgs args) =>
        ViewModel.OfflineExpanded = true;

    private void OfflineExpander_Collapsed(Expander sender, ExpanderCollapsedEventArgs args) =>
        ViewModel.OfflineExpanded = false;

    private async void Search_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.SearchQuery = sender.Text ?? string.Empty;
        if (ViewModel.ShowSearchTab || ViewModel.SearchQuery.Trim().Length >= 2)
        {
            await ViewModel.SearchCommand.ExecuteAsync(null);
        }

        RefreshUi();
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        ViewModel.SearchQuery = sender.Text ?? string.Empty;
        if (ViewModel.ShowFriendsTab)
        {
            RefreshUi();
        }
    }

    private void OpenProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SocialPersonItem person)
        {
            ViewModel.OpenProfileCommand.Execute(person);
        }
    }

    private async void JoinLobby_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SocialPersonItem person)
        {
            await ViewModel.JoinLobbyCommand.ExecuteAsync(person);
        }
    }

    private async void Message_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SocialPersonItem person)
        {
            await ViewModel.OpenChatWithCommand.ExecuteAsync(person);
            RefreshUi();
        }
    }

    private async void OpenThread_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ChatThreadItem thread)
        {
            await ViewModel.OpenChatThreadCommand.ExecuteAsync(thread);
            RefreshUi();
        }
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
