using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.ViewModels;
using SGSClient.UI.WinUI.ViewModels;
using SGSClient.UI.WinUI.Views;

namespace SGSClient.UI.WinUI;

public sealed partial class MainWindow : Window
{
    private string _contentNavTag = "Home";
    private bool _socialOpen;

    public MainWindow()
    {
        InitializeComponent();
        Title = "SGSClient";

        Sidebar.ViewModel.SetNavigateHandler(NavigateByTag);
        Sidebar.ViewModel.SetOpenDetailsHandler(OpenGameDetails);
        Sidebar.ViewModel.SetOpenPropertiesHandler(OpenGameProperties);
        ContentFrame.Navigate(typeof(HomePage));
        _ = Sidebar.InitializeAsync();
    }

    public void RefreshSidebar()
    {
        Sidebar.ViewModel.RefreshProfile();
        _ = Sidebar.ViewModel.LoadInstalledAsync();
    }

    public void SetAdminNavVisible(bool visible)
    {
        Sidebar.ViewModel.SetRoleVisibility(visible, visible);
        Sidebar.ViewModel.RefreshProfile();
    }

    public void NavigateTo(Type pageType, object? parameter = null, string? selectTag = null)
    {
        if (selectTag is not null && selectTag != "Friends")
        {
            _contentNavTag = selectTag;
            CloseSocialOverlay();
            Sidebar.ViewModel.SelectNav(selectTag);
        }
        else if (selectTag is not null)
        {
            Sidebar.ViewModel.SelectNav(selectTag);
        }

        if (pageType == typeof(FriendsPage))
        {
            OpenSocialOverlay(parameter as string);
            return;
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

        if (selectTag is "Library" or "Home" || pageType == typeof(LibraryPage))
        {
            _ = Sidebar.ViewModel.LoadInstalledAsync();
        }
    }

    private void NavigateByTag(string tag)
    {
        if (tag == "Friends")
        {
            if (_socialOpen)
            {
                CloseSocialOverlay();
            }
            else
            {
                OpenSocialOverlay();
            }

            return;
        }

        _contentNavTag = tag;
        CloseSocialOverlay();

        var pageType = tag switch
        {
            "Games" => typeof(GamesPage),
            "Library" => typeof(LibraryPage),
            "Publish" => typeof(PublishPage),
            "Studio" => typeof(StudioPage),
            "Admin" => typeof(AdminPage),
            "Settings" => typeof(SettingsPage),
            "Account" => typeof(AccountPage),
            _ => typeof(HomePage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }

        if (tag is "Library" or "Home")
        {
            _ = Sidebar.ViewModel.LoadInstalledAsync();
        }
    }

    public void OpenSocialOverlay(string? tab = null)
    {
        if (SocialFrame.Content is FriendsPage page)
        {
            _ = page.ViewModel.InitializeAsync(tab);
        }
        else
        {
            SocialFrame.Navigate(typeof(FriendsPage), tab);
        }

        SocialPanel.Translation = new Vector3(0, 0, 16);
        SocialPanel.Visibility = Visibility.Visible;
        _socialOpen = true;
    }

    public void CloseSocialOverlay()
    {
        if (!_socialOpen)
        {
            return;
        }

        SocialPanel.Visibility = Visibility.Collapsed;
        _socialOpen = false;
        Sidebar.ViewModel.SelectNav(_contentNavTag);
    }

    private void OpenGameDetails(int gameId)
    {
        NavigateTo(
            typeof(GameDetailsPage),
            parameter: new GameDetailsNavArgs(gameId, "Library"),
            selectTag: "Library");
    }

    private async void OpenGameProperties(SidebarInstalledGame item)
    {
        var dialog = new GamePropertiesDialog();
        await dialog.ShowForGameAsync(item.GameId, item.BuildId, item.Title, Content.XamlRoot);
        await Sidebar.ViewModel.LoadInstalledAsync();
    }
}
