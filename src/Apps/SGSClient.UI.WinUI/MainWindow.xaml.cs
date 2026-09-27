using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.Core.Interfaces;
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
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));

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
        if (LoginGatePanel.Visibility == Visibility.Visible)
        {
            return;
        }

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

    private void NavigateByTag(string tag) => NavigateByTagPublic(tag);

    public void NavigateByTagPublic(string tag)
    {
        if (LoginGatePanel.Visibility == Visibility.Visible)
        {
            return;
        }

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

        if (tag.StartsWith("Games:genre:", StringComparison.OrdinalIgnoreCase))
        {
            var genre = tag["Games:genre:".Length..];
            var gamesVm = App.Services.GetRequiredService<GamesViewModel>();
            gamesVm.ApplyGenreFilter(genre);
            _contentNavTag = "Games";
            CloseSocialOverlay();
            Sidebar.ViewModel.SelectNav("Games");
            ContentFrame.Navigate(typeof(GamesPage));
            _ = gamesVm.LoadAsync();
            return;
        }

        _contentNavTag = tag;
        CloseSocialOverlay();

        var pageType = tag switch
        {
            "Games" => typeof(GamesPage),
            "Library" => typeof(LibraryPage),
            "Wishlist" => typeof(WishlistPage),
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
        if (LoginGatePanel.Visibility == Visibility.Visible)
        {
            return;
        }

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

    public void ShowLoginGate(LoginGateViewModel gate)
    {
        if (!ReferenceEquals(LoginGatePanel.DataContext, gate))
        {
            if (LoginGatePanel.DataContext is LoginGateViewModel previous)
            {
                previous.PropertyChanged -= OnLoginGatePropertyChanged;
            }

            gate.PropertyChanged += OnLoginGatePropertyChanged;
            LoginGatePanel.DataContext = gate;
        }

        UpdateLoginGateUi(gate);
        ShellRoot.Visibility = Visibility.Collapsed;
        LoginGatePanel.Visibility = Visibility.Visible;
    }

    private void OnLoginGatePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is LoginGateViewModel gate
            && e.PropertyName is nameof(LoginGateViewModel.StatusText) or nameof(LoginGateViewModel.IsBusy))
        {
            UpdateLoginGateUi(gate);
        }
    }

    private void UpdateLoginGateUi(LoginGateViewModel gate)
    {
        LoginStatusText.Text = gate.StatusText;
        LoginVersionText.Text = gate.VersionText;
        LoginButton.IsEnabled = !gate.IsBusy;
        SignUpLink.IsEnabled = !gate.IsBusy;
        LoginCancelLink.Visibility = gate.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        LoginProgress.IsActive = gate.IsBusy;
        LoginProgress.Visibility = gate.IsBusy ? Visibility.Visible : Visibility.Collapsed;
    }

    public void HideLoginGate()
    {
        LoginGatePanel.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Visible;
    }

    private async void LoginButton_OnClick(object sender, RoutedEventArgs e) =>
        await RunLoginGateAsync(signUp: false);

    private async void SignUpLink_OnClick(object sender, RoutedEventArgs e) =>
        await RunLoginGateAsync(signUp: true);

    private void LoginCancelLink_OnClick(object sender, RoutedEventArgs e)
    {
        if (LoginGatePanel.DataContext is LoginGateViewModel gate)
        {
            gate.CancelCommand.Execute(null);
        }
    }

    private async Task RunLoginGateAsync(bool signUp)
    {
        if (LoginGatePanel.DataContext is not LoginGateViewModel gate)
        {
            return;
        }

        gate.SetParentWindowHandle(() =>
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            return hwnd;
        });
        gate.SetOnAuthenticated(async () =>
        {
            HideLoginGate();
            var auth = App.Services.GetService<IAuthSession>();
            if (auth?.CurrentUser is { } profile)
            {
                SetAdminNavVisible(profile.IsAdmin);
            }

            RefreshSidebar();
            var library = App.Services.GetService<LibraryViewModel>();
            if (library is not null)
            {
                await library.LoadAsync();
            }

            await Sidebar.ViewModel.Notifications.StartAsync();
            if (auth?.CurrentUser?.NeedsPlayerTag == true)
            {
                NavigateTo(typeof(AccountPage), selectTag: "Account");
            }
        });
        if (signUp)
        {
            await gate.SignUpCommand.ExecuteAsync(null);
        }
        else
        {
            await gate.LoginCommand.ExecuteAsync(null);
        }
    }
}
