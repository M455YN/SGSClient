using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class LobbyPage : Page
{
    public LobbyViewModel ViewModel { get; }

    public LobbyPage()
    {
        ViewModel = App.Services.GetRequiredService<LobbyViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(ViewModel.Code);
        Clipboard.SetContent(package);
    }

    private void Kick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: LobbySlotItem slot })
        {
            ViewModel.KickCommand.Execute(slot);
        }
    }

    private void Invite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: LobbyFriendItem friend })
        {
            ViewModel.InviteCommand.Execute(friend);
        }
    }
}
