using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.Core.Models;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class GameDetailsPage : Page
{
    public GameDetailsViewModel ViewModel { get; }

    public GameDetailsPage()
    {
        ViewModel = App.Services.GetRequiredService<GameDetailsViewModel>();
        InitializeComponent();
        ViewModel.SetOpenPropertiesHandler(OpenProperties);
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var (gameId, fromLibrary) = e.Parameter switch
        {
            GameDetailsNavArgs args => (args.GameId, args.ReturnTag == "Library"),
            int id => (id, false),
            _ => (0, false)
        };

        if (gameId <= 0)
        {
            EmptyStatus.Text = "Brak identyfikatora gry.";
            ContentScroll.Visibility = Visibility.Collapsed;
            return;
        }

        await ViewModel.LoadAsync(gameId, fromLibrary);
        RefreshUi();
    }

    private void RefreshUi()
    {
        EmptyStatus.Text = ViewModel.StatusText;
        EmptyStatus.Visibility = ViewModel.HasContent ? Visibility.Collapsed : Visibility.Visible;
        ContentScroll.Visibility = ViewModel.HasContent ? Visibility.Visible : Visibility.Collapsed;

        BackButton.Content = ViewModel.BackLabel;
        ContextLabelText.Text = ViewModel.ContextLabel;
        TitleText.Text = ViewModel.Title;
        TaglineText.Text = ViewModel.Tagline;
        TileLabelText.Text = ViewModel.TileLabel;
        StatusText.Text = ViewModel.StatusText;
        DescriptionText.Text = ViewModel.Description;
        HardwareText.Text = ViewModel.HardwareRequirements;
        OtherText.Text = ViewModel.OtherInformation;
        PlatformsText.Text = ViewModel.PlatformsText;
        SymbolText.Text = ViewModel.Symbol;

        PrimaryActionButton.Content = ViewModel.PrimaryActionLabel;
        PrimaryActionButton.IsEnabled = ViewModel.CanAdd;
        PrimaryActionButton.Visibility = ViewModel.ShowStoreActions ? Visibility.Visible : Visibility.Collapsed;

        LaunchButton.Visibility = ViewModel.ShowLibraryActions ? Visibility.Visible : Visibility.Collapsed;
        LaunchButton.IsEnabled = ViewModel.CanLaunch;
        InstallButton.Visibility = ViewModel.ShowLibraryActions ? Visibility.Visible : Visibility.Collapsed;
        InstallButton.IsEnabled = ViewModel.CanInstall;
        UninstallButton.Visibility = ViewModel.ShowLibraryActions ? Visibility.Visible : Visibility.Collapsed;
        UninstallButton.IsEnabled = ViewModel.CanUninstall;
        PropertiesButton.Visibility = ViewModel.ShowLibraryActions ? Visibility.Visible : Visibility.Collapsed;

        InstallProgressPanel.Visibility = ViewModel.IsInstalling ? Visibility.Visible : Visibility.Collapsed;
        InstallProgressText.Text = ViewModel.InstallProgressText;
        InstallProgressBar.Value = ViewModel.InstallProgressFraction;

        BadgesList.ItemsSource = ViewModel.Badges;
        MediaThumbsList.ItemsSource = ViewModel.MediaThumbs;
        BuildsList.ItemsSource = ViewModel.Builds;

        HardwarePanel.Visibility = ViewModel.ShowHardware ? Visibility.Visible : Visibility.Collapsed;
        BuildsPanel.Visibility = ViewModel.ShowBuilds ? Visibility.Visible : Visibility.Collapsed;
        OtherPanel.Visibility = ViewModel.ShowOther ? Visibility.Visible : Visibility.Collapsed;

        DetailsPanel.Visibility = ViewModel.ShowDetailsTab ? Visibility.Visible : Visibility.Collapsed;
        ReviewsPanel.Visibility = ViewModel.ShowReviewsTab ? Visibility.Visible : Visibility.Collapsed;
        MorePanel.Visibility = ViewModel.ShowMoreTab ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        var returnTag = ViewModel.ReturnTag;
        if (returnTag == "Library")
        {
            App.MainWindow?.NavigateTo(typeof(LibraryPage), selectTag: "Library");
            return;
        }

        if (Frame.CanGoBack)
        {
            Frame.GoBack();
            return;
        }

        App.MainWindow?.NavigateTo(typeof(GamesPage), selectTag: "Games");
    }

    private async void AddToLibrary_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddToLibraryCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LaunchCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.InstallCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.UninstallCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void Properties_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenPropertiesCommand.Execute(null);
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string tab)
        {
            ViewModel.SelectTabCommand.Execute(tab);
            RefreshUi();
        }
    }

    private async void OpenProperties(GameLibraryItem item)
    {
        var dialog = new GamePropertiesDialog();
        await dialog.ShowForGameAsync(item.GameId, item.BuildId, item.Title, XamlRoot);
    }
}
