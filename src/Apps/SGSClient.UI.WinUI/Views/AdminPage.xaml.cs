using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class AdminPage : Page
{
    public AdminViewModel ViewModel { get; }

    public AdminPage()
    {
        ViewModel = App.Services.GetRequiredService<AdminViewModel>();
        InitializeComponent();
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
        GamesList.Visibility = ViewModel.ShowGames ? Visibility.Visible : Visibility.Collapsed;
        UsersList.Visibility = ViewModel.ShowUsers ? Visibility.Visible : Visibility.Collapsed;
        GamesList.ItemsSource = null;
        GamesList.ItemsSource = ViewModel.Games;
        UsersList.ItemsSource = null;
        UsersList.ItemsSource = ViewModel.Users;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void GamesTab_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectTabCommand.Execute("games");
        RefreshUi();
    }

    private void UsersTab_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectTabCommand.Execute("users");
        RefreshUi();
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AdminGameItem item })
        {
            await ViewModel.EditCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void ToggleDraft_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AdminGameItem item })
        {
            await ViewModel.ToggleDraftCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void ToggleFeatured_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AdminGameItem item })
        {
            await ViewModel.ToggleFeaturedCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: AdminGameItem item })
        {
            await ViewModel.DeleteCommand.ExecuteAsync(item);
            RefreshUi();
        }
    }

    private async Task<bool> ShowEditDialogAsync(AdminGameItem item)
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
        var draftBox = new CheckBox { Content = "Szkic (ukryty w sklepie)", IsChecked = item.DraftP };
        var featuredBox = new CheckBox
        {
            Content = "Wyróżnione",
            IsChecked = item.FeaturedP,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Tytuł", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(titleBox);
        panel.Children.Add(new TextBlock { Text = "Opis", Opacity = 0.55, FontSize = 12 });
        panel.Children.Add(descBox);
        panel.Children.Add(draftBox);
        panel.Children.Add(featuredBox);

        var dialog = new ContentDialog
        {
            Title = $"Admin — {item.Symbol}",
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
        item.EditFeatured = featuredBox.IsChecked == true;
        return true;
    }

    private async Task<bool> ConfirmDeleteAsync(AdminGameItem item)
    {
        var dialog = new ContentDialog
        {
            Title = "Usuń (admin)",
            Content = $"Na pewno usunąć „{item.Title}”? Operacji nie można cofnąć.",
            PrimaryButtonText = "Usuń",
            CloseButtonText = "Anuluj",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
