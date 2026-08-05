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

    public void NavigateTo(Type pageType, object? parameter = null, string? selectTag = null)
    {
        if (selectTag is not null)
        {
            SelectNavTag(selectTag);
        }

        if (parameter is null)
        {
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
        else
        {
            ContentFrame.Navigate(pageType, parameter);
        }
    }

    private void SelectNavTag(string tag)
    {
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>()
                     .Concat(NavView.FooterMenuItems.OfType<NavigationViewItem>()))
        {
            if (item.Tag as string == tag)
            {
                NavView.SelectedItem = item;
                return;
            }
        }
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
            "Publish" => typeof(PublishPage),
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
