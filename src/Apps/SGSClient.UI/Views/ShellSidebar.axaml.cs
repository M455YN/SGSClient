using SGSClient.Core.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class ShellSidebar : UserControl
{
    private const double ExpandedWidth = 280;
    private const double CollapsedWidth = 48;
    private bool _wired;

    public ShellSidebarViewModel? ViewModel => DataContext as ShellSidebarViewModel;

    public ShellSidebar()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireViewModel();
        AttachedToVisualTree += async (_, _) =>
        {
            WireViewModel();
            if (ViewModel is not null)
            {
                await ViewModel.LoadInstalledAsync();
                RefreshChrome();
            }
        };
    }

    private void WireViewModel()
    {
        if (ViewModel is null || _wired)
        {
            return;
        }

        _wired = true;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ShellSidebarViewModel.SelectedNavTag)
                or nameof(ShellSidebarViewModel.IsAdminVisible)
                or nameof(ShellSidebarViewModel.IsCreatorVisible)
                or nameof(ShellSidebarViewModel.ProfileDisplayName)
                or nameof(ShellSidebarViewModel.ProfileStatus)
                or nameof(ShellSidebarViewModel.HasAvatar)
                or nameof(ShellSidebarViewModel.ProfileAvatarUrl)
                or nameof(ShellSidebarViewModel.InstalledHeader)
                or nameof(ShellSidebarViewModel.IsPaneOpen)
                or null)
            {
                RefreshChrome();
            }
        };
        ViewModel.InstalledGames.CollectionChanged += (_, _) =>
        {
            InstalledList.ItemsSource = null;
            InstalledList.ItemsSource = ViewModel.InstalledGames;
            InstalledHeaderText.Text = ViewModel.InstalledHeader.ToUpperInvariant();
        };
        RefreshChrome();
        InstalledList.ItemsSource = ViewModel.InstalledGames;
    }

    private void RefreshChrome()
    {
        var vm = ViewModel;
        if (vm is null)
        {
            return;
        }

        var expanded = vm.IsPaneOpen;
        Width = expanded ? ExpandedWidth : CollapsedWidth;

        ProfileDetails.IsVisible = expanded;
        InstalledSection.IsVisible = expanded;

        NavHomeLabel.IsVisible = expanded;
        NavGamesLabel.IsVisible = expanded;
        NavLibraryLabel.IsVisible = expanded;
        NavDownloadsLabel.IsVisible = expanded;
        NavWishlistLabel.IsVisible = expanded;
        NavCreativeLabel.IsVisible = expanded;
        NavPublishLabel.IsVisible = expanded;
        NavStudioLabel.IsVisible = expanded;
        NavAdminLabel.IsVisible = expanded;
        NavSettingsLabel.IsVisible = expanded;

        ApplyCompactChrome(PaneToggleButton, expanded, profileLike: false);
        ApplyCompactChrome(ProfileButton, expanded, profileLike: true);
        ApplyCompactChrome(NavHome, expanded, profileLike: false);
        ApplyCompactChrome(NavGames, expanded, profileLike: false);
        ApplyCompactChrome(NavLibrary, expanded, profileLike: false);
        ApplyCompactChrome(NavDownloads, expanded, profileLike: false);
        ApplyCompactChrome(NavWishlist, expanded, profileLike: false);
        ApplyCompactChrome(NavCreative, expanded, profileLike: false);
        ApplyCompactChrome(NavPublish, expanded, profileLike: false);
        ApplyCompactChrome(NavStudio, expanded, profileLike: false);
        ApplyCompactChrome(NavAdmin, expanded, profileLike: false);
        ApplyCompactChrome(NavSettings, expanded, profileLike: false);

        if (expanded)
        {
            ProfileContentGrid.ColumnSpacing = 10;
            ProfileContentGrid.ColumnDefinitions[0].Width = GridLength.Auto;
            ProfileContentGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            AvatarHost.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            ProfileContentGrid.ColumnSpacing = 0;
            ProfileContentGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ProfileContentGrid.ColumnDefinitions[1].Width = new GridLength(0);
            AvatarHost.HorizontalAlignment = HorizontalAlignment.Center;
        }

        var avatar = expanded ? 36.0 : 28.0;
        AvatarHost.Width = avatar;
        AvatarHost.Height = avatar;
        AvatarHost.CornerRadius = new CornerRadius(avatar / 2);
        AvatarImage.Width = avatar;
        AvatarImage.Height = avatar;

        ProfileNameText.Text = vm.ProfileDisplayName;
        ProfileStatusText.Text = vm.ProfileStatus;
        NavAdmin.IsVisible = vm.IsAdminVisible;
        NavPublish.IsVisible = false;
        NavStudio.IsVisible = vm.IsCreatorVisible;
        InstalledHeaderText.Text = vm.InstalledHeader.ToUpperInvariant();
        AvatarBorder.IsVisible = vm.HasAvatar;
        ProfileIcon.IsVisible = !vm.HasAvatar;

        Highlight(NavHome, vm.IsHomeSelected);
        Highlight(NavGames, vm.IsGamesSelected);
        Highlight(NavLibrary, vm.IsLibrarySelected);
        Highlight(NavDownloads, vm.IsDownloadsSelected);
        Highlight(NavWishlist, vm.IsWishlistSelected);
        Highlight(NavCreative, vm.IsCreativeSelected);
        Highlight(NavPublish, vm.IsPublishSelected);
        Highlight(NavStudio, vm.IsStudioSelected);
        Highlight(NavAdmin, vm.IsAdminSelected);
        Highlight(NavSettings, vm.IsSettingsSelected);
    }

    private static void ApplyCompactChrome(Button button, bool expanded, bool profileLike)
    {
        if (expanded)
        {
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Width = double.NaN;
            button.Padding = profileLike ? new Thickness(8) : new Thickness(12, 10);
        }
        else
        {
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.Width = 40;
            button.MinHeight = 40;
            button.Padding = new Thickness(0);
        }
    }

    private void Highlight(Button button, bool selected)
    {
        if (selected)
        {
            button.Background = this.TryFindResource("SystemControlBackgroundListLowBrush", out var brush)
                && brush is IBrush b
                ? b
                : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        }
        else
        {
            button.Background = Brushes.Transparent;
        }
    }

    private void PaneToggle_OnClick(object? sender, RoutedEventArgs e) =>
        ViewModel?.TogglePaneCommand.Execute(null);

    private void Profile_OnClick(object? sender, RoutedEventArgs e) =>
        ViewModel?.OpenProfileCommand.Execute(null);

    private void Nav_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string tag })
        {
            ViewModel?.NavigateCommand.Execute(tag);
        }
    }

    private void Installed_OnDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (InstalledList.SelectedItem is SidebarInstalledGame game)
        {
            ViewModel?.OpenDetailsCommand.Execute(game);
        }
    }

    private async void Play_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: SidebarInstalledGame game } && ViewModel is not null)
        {
            await ViewModel.LaunchCommand.ExecuteAsync(game);
        }
    }

    private async void More_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: SidebarInstalledGame game } button || ViewModel is null)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var details = new MenuItem { Header = Loc.T("Szczegóły") };
        details.Click += (_, _) => ViewModel.OpenDetailsCommand.Execute(game);
        var props = new MenuItem { Header = Loc.T("Właściwości") };
        props.Click += (_, _) => ViewModel.OpenPropertiesCommand.Execute(game);
        flyout.Items.Add(details);
        flyout.Items.Add(props);
        flyout.ShowAt(button);
        await Task.CompletedTask;
    }
}
