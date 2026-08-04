using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.WinUI.Views;

namespace SGSClient.UI.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "SGSClient";
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        var pageType = tag switch
        {
            "Games" => typeof(GamesPage),
            "Library" => typeof(LibraryPage),
            "Settings" => typeof(SettingsPage),
            "Account" => typeof(AccountPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
