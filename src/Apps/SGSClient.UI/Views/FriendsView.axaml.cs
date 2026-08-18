using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class FriendsView : UserControl
{
    public FriendsView()
    {
        InitializeComponent();
    }

    private void Tab_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: string tab })
        {
            vm.SelectTabCommand.Execute(tab);
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
