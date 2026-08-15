using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SGSClient.UI.WinUI.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class GamePropertiesDialog : UserControl
{
    private bool _uninstallArmed;
    private bool _moveArmed;

    public GamePropertiesViewModel ViewModel { get; }

    public GamePropertiesDialog()
    {
        ViewModel = App.Services.GetRequiredService<GamePropertiesViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        NavList.ItemsSource = ViewModel.NavItems;
        RefreshUi();
    }

    public async Task ShowForGameAsync(int gameId, int buildId, string title, XamlRoot xamlRoot)
    {
        await ViewModel.LoadAsync(gameId, buildId, title);
        RefreshUi();

        var selectedNav = ViewModel.NavItems.FirstOrDefault(n => n.Id == ViewModel.SelectedSection);
        if (selectedNav is not null)
        {
            NavList.SelectedItem = selectedNav;
        }

        var dialog = new ContentDialog
        {
            Title = $"Właściwości — {title}",
            Content = this,
            CloseButtonText = "Zamknij",
            XamlRoot = xamlRoot,
            DefaultButton = ContentDialogButton.Close,
            FullSizeDesired = true
        };

        // ContentDialog defaults to ~548px max width; Steam-like properties need more room.
        dialog.Resources["ContentDialogMaxWidth"] = 1200.0;
        dialog.Resources["ContentDialogMinWidth"] = 1000.0;
        dialog.Resources["ContentDialogMaxHeight"] = 820.0;

        await dialog.ShowAsync();
    }

    private void RefreshUi()
    {
        GameTitleText.Text = ViewModel.GameTitle;
        VersionsList.ItemsSource = ViewModel.Versions;
        StatusText.Text = ViewModel.StatusText;
        BetasPanel.Visibility = ViewModel.ShowBetas ? Visibility.Visible : Visibility.Collapsed;
        FilesPanel.Visibility = ViewModel.ShowFiles ? Visibility.Visible : Visibility.Collapsed;
        PersonalizationPanel.Visibility = ViewModel.ShowPersonalization ? Visibility.Visible : Visibility.Collapsed;
        DlcPanel.Visibility = ViewModel.ShowDlc ? Visibility.Visible : Visibility.Collapsed;
        PlaceholderPanel.Visibility = ViewModel.ShowPlaceholder ? Visibility.Visible : Visibility.Collapsed;
        DlcList.ItemsSource = ViewModel.FilteredDlcRows;
        DlcStatusText.Text = ViewModel.DlcStatusText;
        if (DlcSearchBox.Text != ViewModel.DlcSearchText)
        {
            DlcSearchBox.Text = ViewModel.DlcSearchText;
        }
        if (PlaceholderInfo is not null)
        {
            PlaceholderInfo.Message = ViewModel.PlaceholderText;
        }

        InstallStateText.Text = ViewModel.InstallStateText;
        InstalledVersionText.Text = ViewModel.InstalledVersionText;
        InstallSizeText.Text = ViewModel.InstallSizeText;
        InstallFilesText.Text = ViewModel.InstallFilesText;
        InstallPathText.Text = ViewModel.InstallPathText;
        BrowseFilesButton.IsEnabled = ViewModel.CanBrowseFiles;
        UninstallFilesButton.IsEnabled = ViewModel.CanUninstallFiles && !ViewModel.IsUninstalling;
        if (!ViewModel.CanUninstallFiles)
        {
            _uninstallArmed = false;
        }

        UninstallFilesButton.Content = _uninstallArmed ? "Potwierdź odinstalowanie" : "Odinstaluj…";

        MoveFilesPanel.Visibility = ViewModel.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        MoveTargetCombo.ItemsSource = ViewModel.MoveTargets;
        if (ViewModel.SelectedMoveTarget is not null
            && !ReferenceEquals(MoveTargetCombo.SelectedItem, ViewModel.SelectedMoveTarget))
        {
            MoveTargetCombo.SelectedItem = ViewModel.SelectedMoveTarget;
        }

        MoveFilesButton.IsEnabled = ViewModel.CanMoveFiles && !ViewModel.IsMoving;
        MoveFilesButton.Content = _moveArmed ? "Potwierdź przeniesienie" : "Przenieś pliki…";
        if (!ViewModel.CanMoveFiles)
        {
            _moveArmed = false;
            MoveFilesButton.Content = "Przenieś pliki…";
        }

        MoveProgressText.Text = ViewModel.MoveProgressText;

        ClearCustomCoverButton.IsEnabled = ViewModel.HasCustomCover;
        if (ViewModel.HasCustomCover && !string.IsNullOrWhiteSpace(ViewModel.CustomCoverPreviewUri))
        {
            CustomCoverImage.Source = new BitmapImage(new Uri(ViewModel.CustomCoverPreviewUri));
            CustomCoverImage.Visibility = Visibility.Visible;
            CustomCoverPlaceholder.Visibility = Visibility.Collapsed;
        }
        else
        {
            CustomCoverImage.Source = null;
            CustomCoverImage.Visibility = Visibility.Collapsed;
            CustomCoverPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is PropertiesNavItem item)
        {
            ViewModel.SelectSectionCommand.Execute(item.Id);
            RefreshUi();
        }
    }

    private void Version_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionsList.SelectedItem is BetaVersionItem item)
        {
            ViewModel.SelectVersionCommand.Execute(item);
            RefreshUi();
        }
    }

    private async void PickCustomCover_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.CommitButtonText = "Użyj jako kafel";
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp" })
        {
            picker.FileTypeFilter.Add(ext);
        }

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        await ViewModel.ApplyCustomCoverFromFileAsync(file.Path);
        RefreshUi();
    }

    private async void ClearCustomCover_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ClearCustomCoverCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void DlcSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.DlcSearchText = DlcSearchBox.Text ?? string.Empty;
        RefreshUi();
    }

    private async void DlcInstallCheck_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || box.Tag is not DlcRowItem row)
        {
            return;
        }

        var wantInstalled = box.IsChecked == true;
        await ViewModel.SetDlcInstalledAsync(row, wantInstalled);
        box.IsChecked = row.IsInstalled;
        RefreshUi();
    }

    private void ViewMoreInStore_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ViewMoreInStoreCommand.Execute(null);
        App.MainWindow?.NavigateTo(typeof(GameDetailsPage), ViewModel.GameId, "Games");
    }

    private void BrowseLocalFiles_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BrowseLocalFilesCommand.Execute(null);
        RefreshUi();
    }

    private async void RefreshInstallInfo_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshInstallInfoCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void UninstallFiles_Click(object sender, RoutedEventArgs e)
    {
        // Nested ContentDialog is not allowed while properties dialog is open.
        if (!_uninstallArmed)
        {
            _uninstallArmed = true;
            ViewModel.StatusText = "Kliknij ponownie, aby usunąć lokalne pliki.";
            RefreshUi();
            return;
        }

        _uninstallArmed = false;
        await ViewModel.UninstallFilesCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void MoveTargetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MoveTargetCombo.SelectedItem is MoveTargetFolderItem item)
        {
            ViewModel.SelectedMoveTarget = item;
        }
    }

    private async void MoveFiles_Click(object sender, RoutedEventArgs e)
    {
        if (!_moveArmed)
        {
            _moveArmed = true;
            ViewModel.StatusText = "Kliknij ponownie, aby przenieść pliki do wybranego folderu.";
            RefreshUi();
            return;
        }

        _moveArmed = false;
        await ViewModel.MoveFilesCommand.ExecuteAsync(null);
        RefreshUi();
    }
}
