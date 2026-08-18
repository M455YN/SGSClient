using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class FriendsView : UserControl
{
    public FriendsView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? Shell =>
        VisualRoot is Window { DataContext: MainWindowViewModel main } ? main : null;

    private void Tab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: string tab })
        {
            vm.SelectTabCommand.Execute(tab);
        }
    }

    private void OverlayProfile_OnClick(object? sender, RoutedEventArgs e)
    {
        Shell?.CloseSocial();
        Shell?.NavigateCommand.Execute("Account");
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e) =>
        Shell?.CloseSocial();

    private void ToggleIncoming_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm)
        {
            vm.IncomingExpanded = !vm.IncomingExpanded;
        }
    }

    private void ToggleOnline_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm)
        {
            vm.OnlineExpanded = !vm.OnlineExpanded;
        }
    }

    private void ToggleOffline_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm)
        {
            vm.OfflineExpanded = !vm.OfflineExpanded;
        }
    }

    private async void SearchBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not FriendsViewModel vm)
        {
            return;
        }

        if (vm.ShowSearchTab || vm.SearchQuery.Trim().Length >= 2)
        {
            await vm.SearchCommand.ExecuteAsync(null);
        }
    }

    private async void AddFriend_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: SocialPersonItem person })
        {
            await vm.AddFriendCommand.ExecuteAsync(person);
        }
    }

    private async void Accept_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: SocialPersonItem person })
        {
            await vm.AcceptFriendCommand.ExecuteAsync(person);
        }
    }

    private async void RemoveFriend_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: SocialPersonItem person })
        {
            await vm.RemoveFriendCommand.ExecuteAsync(person);
        }
    }

    private async void Follow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: SocialPersonItem person })
        {
            await vm.FollowCommand.ExecuteAsync(person);
        }
    }

    private async void Unfollow_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: SocialPersonItem person })
        {
            await vm.UnfollowCommand.ExecuteAsync(person);
        }
    }
}
