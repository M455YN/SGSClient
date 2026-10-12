using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.UI.ViewModels;
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
        PlaceDefaultWindow();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        Activated += (_, e) =>
            AppTitleText.Opacity = e.WindowActivationState == WindowActivationState.Deactivated ? 0.5 : 1;

        Sidebar.ViewModel.SetNavigateHandler(NavigateByTag);
        Sidebar.ViewModel.SetOpenDetailsHandler(OpenGameDetails);
        Sidebar.ViewModel.SetOpenPropertiesHandler(OpenGameProperties);
        ContentFrame.Navigate(typeof(HomePage));
        _ = Sidebar.InitializeAsync();
        WireDownloadStatusBar();
        WireNotificationsStatusBar();
        WireOfflineBanner();
    }

    /// <summary>
    /// AccountViewModel is transient (one per page), so every instance that can change the profile
    /// is attached here: the shell refreshes its sidebar on profile / role changes.
    /// </summary>
    public void AttachAccount(AccountViewModel account)
    {
        account.ProfileChanged += RefreshSidebar;
        account.AdminVisibilityChanged += SetAdminNavVisible;
    }

    private void PlaceDefaultWindow()
    {
        const int width = 1480;
        const int height = 768;
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
        var work = area.WorkArea;
        var w = Math.Min(width, work.Width);
        var h = Math.Min(height, work.Height);
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(w, h));
        AppWindow.Move(new global::Windows.Graphics.PointInt32(
            work.X + (work.Width - w) / 2,
            work.Y + (work.Height - h) / 2));
    }

    private void WireOfflineBanner()
    {
        var network = App.Services.GetService<INetworkStatusService>();
        if (network is null)
        {
            return;
        }

        void Refresh()
        {
            OfflineBanner.Visibility = network.IsOnline ? Visibility.Collapsed : Visibility.Visible;
        }

        network.Changed += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(Refresh);
        };
        Refresh();
    }

    private void WireNotificationsStatusBar()
    {
        var notifications = Sidebar.ViewModel.Notifications;
        void Refresh()
        {
            NotificationsBadge.Visibility = notifications.HasUnread ? Visibility.Visible : Visibility.Collapsed;
            NotificationsBadgeText.Text = notifications.UnreadCount > 99
                ? "99+"
                : notifications.UnreadCount.ToString();
            NotificationsList.ItemsSource = notifications.Items;
        }

        notifications.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(NotificationsViewModel.HasUnread)
                or nameof(NotificationsViewModel.UnreadCount)
                or null)
            {
                Refresh();
            }
        };
        notifications.Items.CollectionChanged += (_, _) => Refresh();
        Refresh();
    }

    private async void MarkAllNotificationsRead_OnClick(object sender, RoutedEventArgs e)
    {
        await Sidebar.ViewModel.Notifications.MarkAllReadCommand.ExecuteAsync(null);
        NotificationsList.ItemsSource = null;
        NotificationsList.ItemsSource = Sidebar.ViewModel.Notifications.Items;
        NotificationsBadge.Visibility = Sidebar.ViewModel.Notifications.HasUnread
            ? Visibility.Visible
            : Visibility.Collapsed;
        NotificationsBadgeText.Text = Sidebar.ViewModel.Notifications.UnreadCount.ToString();
    }

    private async void NotificationItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NotificationDto item })
        {
            await Sidebar.ViewModel.Notifications.MarkReadCommand.ExecuteAsync(item);
            NotificationsList.ItemsSource = null;
            NotificationsList.ItemsSource = Sidebar.ViewModel.Notifications.Items;
            NotificationsBadge.Visibility = Sidebar.ViewModel.Notifications.HasUnread
                ? Visibility.Visible
                : Visibility.Collapsed;
            NotificationsBadgeText.Text = Sidebar.ViewModel.Notifications.UnreadCount.ToString();
        }
    }

    private void WireDownloadStatusBar()
    {
        var downloads = App.Services.GetService<DownloadsViewModel>();
        if (downloads is null)
        {
            return;
        }

        void Refresh()
        {
            var active = downloads.HasActiveDownload;
            DownloadStatusActivePanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            DownloadStatusIdleText.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
            DownloadStatusTitle.Text = string.IsNullOrWhiteSpace(downloads.ActiveDownloadDetail)
                ? downloads.ActiveDownloadTitle
                : $"{downloads.ActiveDownloadTitle} — {downloads.ActiveDownloadDetail}";
            DownloadStatusProgress.Value = downloads.ActiveDownloadProgress;
            DownloadStatusPercent.Text = active ? downloads.ActiveDownloadProgressText : string.Empty;
        }

        downloads.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DownloadsViewModel.HasActiveDownload)
                or nameof(DownloadsViewModel.ActiveDownloadTitle)
                or nameof(DownloadsViewModel.ActiveDownloadDetail)
                or nameof(DownloadsViewModel.ActiveDownloadProgress)
                or nameof(DownloadsViewModel.ActiveDownloadProgressText)
                or null)
            {
                Refresh();
            }
        };
        downloads.Items.CollectionChanged += (_, _) => Refresh();
        Refresh();
    }

    private void DownloadStatusButton_OnClick(object sender, RoutedEventArgs e) =>
        NavigateByTagPublic("Downloads");

    private void FriendsStatusButton_OnClick(object sender, RoutedEventArgs e) =>
        NavigateByTagPublic("Friends");

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
            "Downloads" => typeof(DownloadsPage),
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
