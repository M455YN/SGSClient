using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.WinUI.ViewModels;
using SGSClient.UI.WinUI.Views;

namespace SGSClient.UI.WinUI;

public sealed partial class MainWindow : Window
{
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
        Sidebar.ViewModel.SetAdminVisible(visible);
        Sidebar.ViewModel.RefreshProfile();
    }

    public void NavigateTo(Type pageType, object? parameter = null, string? selectTag = null)
    {
        if (selectTag is not null)
        {
            Sidebar.ViewModel.SelectNav(selectTag);
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
        var pageType = tag switch
        {
            "Games" => typeof(GamesPage),
            "Library" => typeof(LibraryPage),
            "Publish" => typeof(PublishPage),
            "Studio" => typeof(StudioPage),
            "Admin" => typeof(AdminPage),
            "Settings" => typeof(SettingsPage),
            "Account" => typeof(AccountPage),
            "Friends" => typeof(FriendsPage),
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
