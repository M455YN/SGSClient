using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class StudioPage : Page
{
    public StudioViewModel ViewModel { get; }

    public StudioPage()
    {
        ViewModel = App.Services.GetRequiredService<StudioViewModel>();
        InitializeComponent();
        ViewModel.SetAddGameHandler(NavigateAddGame);
        ViewModel.SetAddDlcHandler(NavigateAddDlc);
        ViewModel.SetEditHandler(ShowEditDialogAsync);
        ViewModel.SetConfirmDeleteHandler(ConfirmDeleteAsync);
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        TitleText.Text = ViewModel.Title;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
        RefreshUi();
    }

    private void RefreshUi()
    {
        StatusText.Text = ViewModel.StatusText;
        EmptyText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        GamesList.Visibility = ViewModel.HasGames ? Visibility.Visible : Visibility.Collapsed;
        GamesList.ItemsSource = null;
        GamesList.ItemsSource = ViewModel.Games;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void AddGame_Click(object sender, RoutedEventArgs e) => ViewModel.AddGameCommand.Execute(null);

    private void AddDlc_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioContentItem item })
        {
            ViewModel.AddDlcCommand.Execute(item);
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioContentItem item })
        {
            await ViewModel.EditCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioContentItem item })
        {
            await ViewModel.DeleteCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private void NavigateAddGame()
    {
        App.MainWindow?.NavigateTo(typeof(PublishPage), parameter: null, selectTag: "Publish");
    }

    private void NavigateAddDlc(string parentSymbol)
    {
        App.MainWindow?.NavigateTo(typeof(PublishPage), parentSymbol, "Publish");
    }

    private async Task<bool> ShowEditDialogAsync(StudioContentItem item)
    {
        var titleBox = new TextBox
        {
            Text = item.Title,
            PlaceholderText = "Tytuł",
            Margin = new Thickness(0, 0, 0, 12)
        };
        var descBox = new TextBox
        {
            Text = item.Description ?? string.Empty,
            PlaceholderText = "Opis",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 96,
            Margin = new Thickness(0, 0, 0, 12)
        };
        var draftBox = new CheckBox
        {
            Content = "Szkic (ukryty w sklepie)",
            IsChecked = item.DraftP
        };

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Tytuł", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(titleBox);
        panel.Children.Add(new TextBlock { Text = "Opis", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(descBox);
        panel.Children.Add(draftBox);

        var dialog = new ContentDialog
        {
            Title = item.IsDlc ? $"Edytuj DLC — {item.Symbol}" : $"Edytuj grę — {item.Symbol}",
            PrimaryButtonText = "Zapisz",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = panel
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return false;
        }

        item.EditTitle = titleBox.Text ?? string.Empty;
        item.EditDescription = descBox.Text;
        item.EditDraft = draftBox.IsChecked == true;
        return true;
    }

    private async Task<bool> ConfirmDeleteAsync(StudioContentItem item)
    {
        var dialog = new ContentDialog
        {
            Title = "Usuń treść",
            Content = $"Czy na pewno usunąć „{item.Title}”? Tej operacji nie można cofnąć.",
            PrimaryButtonText = "Usuń",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
