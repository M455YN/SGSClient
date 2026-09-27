using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class ShellSidebar : UserControl
{
    // CompactPaneLength ≈ NavigationView default rail
    private const double ExpandedWidth = 280;
    private const double CollapsedWidth = 48;

    public ShellSidebarViewModel ViewModel { get; }

    public ShellSidebar()
    {
        ViewModel = App.Services.GetRequiredService<ShellSidebarViewModel>();
        InitializeComponent();
        ViewModel.InstalledGames.CollectionChanged += (_, _) => RefreshInstalledList();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ShellSidebarViewModel.SelectedNavTag)
                or nameof(ShellSidebarViewModel.IsAdminVisible)
                or nameof(ShellSidebarViewModel.IsCreatorVisible)
                or nameof(ShellSidebarViewModel.ProfileDisplayName)
                or nameof(ShellSidebarViewModel.ProfileStatus)
                or nameof(ShellSidebarViewModel.ProfileAvatarUrl)
                or nameof(ShellSidebarViewModel.HasAvatar)
                or nameof(ShellSidebarViewModel.InstalledHeader)
                or nameof(ShellSidebarViewModel.IsPaneOpen)
                or nameof(ShellSidebarViewModel.IsWishlistSelected)
                or nameof(ShellSidebarViewModel.IsDownloadsSelected)
                or null)
            {
                RefreshChrome();
            }
        };
        RefreshChrome();
        RefreshInstalledList();
    }

    public async Task InitializeAsync()
    {
        ViewModel.RefreshProfile();
        RefreshChrome();
        await ViewModel.LoadInstalledAsync();
        RefreshInstalledList();
    }

    private void RefreshChrome()
    {
        var expanded = ViewModel.IsPaneOpen;
        RootGrid.Width = expanded ? ExpandedWidth : CollapsedWidth;
        PaneToggleIcon.Glyph = "\uE700"; // GlobalNavButton (hamburger) — Fluent standard

        ProfileDetails.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        InstalledSection.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

        SetLabelVisibility(NavHomeLabel, expanded);
        SetLabelVisibility(NavGamesLabel, expanded);
        SetLabelVisibility(NavLibraryLabel, expanded);
        SetLabelVisibility(NavDownloadsLabel, expanded);
        SetLabelVisibility(NavWishlistLabel, expanded);
        SetLabelVisibility(NavPublishLabel, expanded);
        SetLabelVisibility(NavStudioLabel, expanded);
        SetLabelVisibility(NavAdminLabel, expanded);
        SetLabelVisibility(NavSettingsLabel, expanded);

        ApplyCompactChrome(ProfileButton, expanded, isProfile: true);
        ApplyCompactChrome(PaneToggleButton, expanded, isProfile: false);
        ApplyCompactChrome(NavHome, expanded, isProfile: false);
        ApplyCompactChrome(NavGames, expanded, isProfile: false);
        ApplyCompactChrome(NavLibrary, expanded, isProfile: false);
        ApplyCompactChrome(NavDownloads, expanded, isProfile: false);
        ApplyCompactChrome(NavWishlist, expanded, isProfile: false);
        ApplyCompactChrome(NavPublish, expanded, isProfile: false);
        ApplyCompactChrome(NavStudio, expanded, isProfile: false);
        ApplyCompactChrome(NavAdmin, expanded, isProfile: false);
        ApplyCompactChrome(NavSettings, expanded, isProfile: false);

        if (expanded)
        {
            ProfileContentGrid.ColumnSpacing = 10;
            AvatarColumn.Width = GridLength.Auto;
            DetailsColumn.Width = new GridLength(1, GridUnitType.Star);
            AvatarHost.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            ProfileContentGrid.ColumnSpacing = 0;
            AvatarColumn.Width = new GridLength(1, GridUnitType.Star);
            DetailsColumn.Width = new GridLength(0);
            AvatarHost.HorizontalAlignment = HorizontalAlignment.Center;
        }

        AvatarHost.Width = expanded ? 36 : 28;
        AvatarHost.Height = expanded ? 36 : 28;
        AvatarHost.CornerRadius = new CornerRadius(expanded ? 18 : 14);
        AvatarPicture.Width = AvatarHost.Width;
        AvatarPicture.Height = AvatarHost.Height;

        ProfileNameText.Text = ViewModel.ProfileDisplayName;
        ProfileStatusText.Text = ViewModel.ProfileStatus;
        NavAdmin.Visibility = ViewModel.IsAdminVisible ? Visibility.Visible : Visibility.Collapsed;
        NavPublish.Visibility = Visibility.Collapsed;
        NavStudio.Visibility = ViewModel.IsCreatorVisible ? Visibility.Visible : Visibility.Collapsed;
        InstalledHeaderText.Text = ViewModel.InstalledHeader.ToUpperInvariant();

        if (ViewModel.HasAvatar
            && !string.IsNullOrWhiteSpace(ViewModel.ProfileAvatarUrl)
            && Uri.TryCreate(ViewModel.ProfileAvatarUrl, UriKind.Absolute, out var avatarUri))
        {
            AvatarPicture.ProfilePicture = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(avatarUri);
            AvatarPicture.Visibility = Visibility.Visible;
            ProfilePlaceholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            AvatarPicture.Visibility = Visibility.Collapsed;
            ProfilePlaceholder.Visibility = Visibility.Visible;
        }

        HighlightNav(NavHome, ViewModel.IsHomeSelected);
        HighlightNav(NavGames, ViewModel.IsGamesSelected);
        HighlightNav(NavLibrary, ViewModel.IsLibrarySelected);
        HighlightNav(NavDownloads, ViewModel.IsDownloadsSelected);
        HighlightNav(NavWishlist, ViewModel.IsWishlistSelected);
        HighlightNav(NavPublish, ViewModel.IsPublishSelected);
        HighlightNav(NavStudio, ViewModel.IsStudioSelected);
        HighlightNav(NavAdmin, ViewModel.IsAdminSelected);
        HighlightNav(NavSettings, ViewModel.IsSettingsSelected);
    }

    private static void SetLabelVisibility(TextBlock label, bool expanded) =>
        label.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;

    private static void ApplyCompactChrome(Button button, bool expanded, bool isProfile)
    {
        if (expanded)
        {
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = isProfile ? new Thickness(8, 8, 8, 8) : new Thickness(12, 10, 12, 10);
            button.ClearValue(WidthProperty);
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            button.Padding = new Thickness(0);
            button.Width = 40;
            button.HorizontalAlignment = HorizontalAlignment.Center;
            button.MinHeight = 40;
        }
    }

    private static void HighlightNav(Button button, bool selected)
    {
        button.Background = selected
            ? TryGetBrush("SubtleFillColorSecondaryBrush")
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        var fg = selected
            ? TryGetBrush("AccentTextFillColorPrimaryBrush")
            : TryGetBrush("TextFillColorPrimaryBrush");

        if (button.Content is StackPanel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is FontIcon icon)
                {
                    icon.Foreground = fg;
                }
                else if (child is TextBlock text)
                {
                    text.Foreground = fg;
                }
            }
        }
        else if (button.Content is FontIcon alone)
        {
            alone.Foreground = fg;
        }
    }

    private static Brush TryGetBrush(string key)
    {
        if (Application.Current?.Resources.TryGetValue(key, out var value) == true)
        {
            if (value is Brush brush)
            {
                return brush;
            }

            if (value is Windows.UI.Color color)
            {
                return new SolidColorBrush(color);
            }
        }

        return new SolidColorBrush(Microsoft.UI.Colors.White);
    }

    private void RefreshInstalledList()
    {
        InstalledList.ItemsSource = null;
        InstalledList.ItemsSource = ViewModel.InstalledGames;
        InstalledHeaderText.Text = ViewModel.InstalledHeader.ToUpperInvariant();
    }

    private void PaneToggle_Click(object sender, RoutedEventArgs e) =>
        ViewModel.TogglePaneCommand.Execute(null);

    private void Profile_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenProfileCommand.Execute(null);

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            ViewModel.NavigateCommand.Execute(tag);
        }
    }

    private void Installed_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SidebarInstalledGame game)
        {
            ViewModel.OpenDetailsCommand.Execute(game);
        }
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SidebarInstalledGame game })
        {
            await ViewModel.LaunchCommand.ExecuteAsync(game);
        }
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        // Flyout opens from XAML.
    }

    private void DetailsMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SidebarInstalledGame game })
        {
            ViewModel.OpenDetailsCommand.Execute(game);
        }
    }

    private void PropertiesMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SidebarInstalledGame game })
        {
            ViewModel.OpenPropertiesCommand.Execute(game);
        }
    }
}
