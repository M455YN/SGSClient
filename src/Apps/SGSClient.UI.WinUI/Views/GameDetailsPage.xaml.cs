using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class GameDetailsPage : Page
{
    public GameDetailsViewModel ViewModel { get; }

    public GameDetailsPage()
    {
        ViewModel = App.Services.GetRequiredService<GameDetailsViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is int gameId)
        {
            await ViewModel.LoadAsync(gameId);
            RefreshUi();
        }
        else
        {
            EmptyStatus.Text = "Brak identyfikatora gry.";
            ContentScroll.Visibility = Visibility.Collapsed;
        }
    }

    private void RefreshUi()
    {
        EmptyStatus.Text = ViewModel.StatusText;
        EmptyStatus.Visibility = ViewModel.HasContent ? Visibility.Collapsed : Visibility.Visible;
        ContentScroll.Visibility = ViewModel.HasContent ? Visibility.Visible : Visibility.Collapsed;

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
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(GamesPage));
        }
    }

    private async void AddToLibrary_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddToLibraryCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string tab)
        {
            ViewModel.SelectTabCommand.Execute(tab);
            RefreshUi();
        }
    }
}
