using Avalonia.Controls;
using SGSClient.Core.Localization;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class FriendsView : UserControl
{
    private FriendsViewModel? _wiredVm;

    public FriendsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is FriendsViewModel vm && !ReferenceEquals(vm, _wiredVm))
            {
                _wiredVm = vm;
                vm.SetShowChatHandler(ShowChatAsync);
            }
        };
    }

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

    private void OpenProfile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: SocialPersonItem person })
        {
            vm.OpenProfileCommand.Execute(person);
        }
    }

    private async void JoinLobby_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: SocialPersonItem person })
        {
            await vm.JoinLobbyCommand.ExecuteAsync(person);
        }
    }

    private async void Message_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: SocialPersonItem person })
        {
            await vm.OpenChatWithCommand.ExecuteAsync(person);
        }
    }

    private async void OpenThread_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FriendsViewModel vm && sender is Control { Tag: ChatThreadItem thread })
        {
            await vm.OpenChatThreadCommand.ExecuteAsync(thread);
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
