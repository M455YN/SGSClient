using SGSClient.Core.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class PublishPage : Page
{
    public PublishViewModel ViewModel { get; }

    public PublishPage()
    {
        ViewModel = App.Services.GetRequiredService<PublishViewModel>();
        InitializeComponent();
        PlatformCombo.ItemsSource = ViewModel.PlatformOptions;
        ArchitectureCombo.ItemsSource = ViewModel.ArchitectureOptions;
        ParentSymbolCombo.ItemsSource = ViewModel.ParentGameOptions;
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        TitleText.Text = ViewModel.Title;
        RefreshUi();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string parentSymbol && !string.IsNullOrWhiteSpace(parentSymbol))
        {
            ViewModel.PrepareForDlc(parentSymbol);
        }

        ViewModel.RefreshAuthHint();
        PushFormFromViewModel();
        RefreshUi();
    }

    private void RefreshUi()
    {
        AuthHintText.Text = ViewModel.AuthHint;
        FolderText.Text = string.IsNullOrWhiteSpace(ViewModel.SelectedFolder)
            ? Loc.T("Nie wybrano folderu.")
            : ViewModel.SelectedFolder;
        StatusText.Text = ViewModel.StatusText;
        SummaryTitleText.Text = ViewModel.SummaryTitle;
        SummaryMetaText.Text = ViewModel.SummaryMeta;
        FormBorder.Visibility = ViewModel.HasManifestForm ? Visibility.Visible : Visibility.Collapsed;
        SummaryBorder.Visibility = ViewModel.HasSummary ? Visibility.Visible : Visibility.Collapsed;
        PublishButton.IsEnabled = ViewModel.CanPublish && !ViewModel.IsBusy;
        PickFolderButton.IsEnabled = !ViewModel.IsBusy;
        PrepareButton.IsEnabled = !ViewModel.IsBusy && ViewModel.HasManifestForm;
        ProgressPanel.Visibility = ViewModel.ShowProgress ? Visibility.Visible : Visibility.Collapsed;
        UploadProgress.Value = ViewModel.Progress;
        IssuesList.ItemsSource = ViewModel.Issues.ToList();
        ParentSymbolPanel.Visibility = ViewModel.ShowParentField ? Visibility.Visible : Visibility.Collapsed;
        ParentSymbolCombo.ItemsSource = ViewModel.ParentGameOptions;

        if (KindGameRadio.IsChecked != !ViewModel.IsDlc)
        {
            KindGameRadio.IsChecked = !ViewModel.IsDlc;
        }

        if (KindDlcRadio.IsChecked != ViewModel.IsDlc)
        {
            KindDlcRadio.IsChecked = ViewModel.IsDlc;
        }

        if (ViewModel.HasManifestForm)
        {
            PushFormFromViewModel();
        }
    }

    private void ContentKind_Changed(object sender, RoutedEventArgs e)
    {
        if (KindDlcRadio.IsChecked == true)
        {
            ViewModel.IsDlc = true;
        }
        else if (KindGameRadio.IsChecked == true)
        {
            ViewModel.IsDlc = false;
        }

        ParentSymbolPanel.Visibility = ViewModel.ShowParentField ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PushFormFromViewModel()
    {
        if (GameTitleBox.Text != ViewModel.GameTitle)
        {
            GameTitleBox.Text = ViewModel.GameTitle;
        }

        if (GameSymbolBox.Text != ViewModel.GameSymbol)
        {
            GameSymbolBox.Text = ViewModel.GameSymbol;
        }

        if (GameDescriptionBox.Text != ViewModel.GameDescription)
        {
            GameDescriptionBox.Text = ViewModel.GameDescription;
        }

        if (HardwareBox.Text != ViewModel.HardwareRequirements)
        {
            HardwareBox.Text = ViewModel.HardwareRequirements;
        }

        if (OtherInfoBox.Text != ViewModel.OtherInformation)
        {
            OtherInfoBox.Text = ViewModel.OtherInformation;
        }

        if (CoverBox.Text != ViewModel.MediaCover)
        {
            CoverBox.Text = ViewModel.MediaCover;
        }

        if (HeaderBox.Text != ViewModel.MediaHeader)
        {
            HeaderBox.Text = ViewModel.MediaHeader;
        }

        if (LogoBox.Text != ViewModel.MediaLogo)
        {
            LogoBox.Text = ViewModel.MediaLogo;
        }

        if (IntroBox.Text != ViewModel.MediaIntro)
        {
            IntroBox.Text = ViewModel.MediaIntro;
        }

        if (ScreenshotsBox.Text != ViewModel.MediaScreenshots)
        {
            ScreenshotsBox.Text = ViewModel.MediaScreenshots;
        }

        if (VersionBox.Text != ViewModel.BuildVersion)
        {
            VersionBox.Text = ViewModel.BuildVersion;
        }

        if (ReleaseNotesBox.Text != ViewModel.ReleaseNotes)
        {
            ReleaseNotesBox.Text = ViewModel.ReleaseNotes;
        }

        if (ExecutableBox.Text != ViewModel.BuildExecutable)
        {
            ExecutableBox.Text = ViewModel.BuildExecutable;
        }

        if (!Equals(PlatformCombo.SelectedItem, ViewModel.BuildPlatform))
        {
            PlatformCombo.SelectedItem = ViewModel.BuildPlatform;
        }

        if (!Equals(ArchitectureCombo.SelectedItem, ViewModel.BuildArchitecture))
        {
            ArchitectureCombo.SelectedItem = ViewModel.BuildArchitecture;
        }

        var parentText = ParentSymbolCombo.Text ?? string.Empty;
        if (!string.Equals(parentText, ViewModel.ParentSymbol, StringComparison.Ordinal))
        {
            ParentSymbolCombo.Text = ViewModel.ParentSymbol;
        }

        if (!Equals(ParentSymbolCombo.SelectedItem, ViewModel.ParentSymbol)
            && ViewModel.ParentGameOptions.Contains(ViewModel.ParentSymbol))
        {
            ParentSymbolCombo.SelectedItem = ViewModel.ParentSymbol;
        }
    }

    private void PullFormIntoViewModel()
    {
        ViewModel.IsDlc = KindDlcRadio.IsChecked == true;
        ViewModel.ParentSymbol = ParentSymbolCombo.Text?.Trim()
            ?? ParentSymbolCombo.SelectedItem as string
            ?? string.Empty;
        ViewModel.GameTitle = GameTitleBox.Text ?? string.Empty;
        ViewModel.GameSymbol = GameSymbolBox.Text ?? string.Empty;
        ViewModel.GameDescription = GameDescriptionBox.Text ?? string.Empty;
        ViewModel.HardwareRequirements = HardwareBox.Text ?? string.Empty;
        ViewModel.OtherInformation = OtherInfoBox.Text ?? string.Empty;
        ViewModel.MediaCover = CoverBox.Text ?? string.Empty;
        ViewModel.MediaHeader = HeaderBox.Text ?? string.Empty;
        ViewModel.MediaLogo = LogoBox.Text ?? string.Empty;
        ViewModel.MediaIntro = IntroBox.Text ?? string.Empty;
        ViewModel.MediaScreenshots = ScreenshotsBox.Text ?? string.Empty;
        ViewModel.BuildVersion = VersionBox.Text ?? string.Empty;
        ViewModel.ReleaseNotes = ReleaseNotesBox.Text ?? string.Empty;
        ViewModel.BuildExecutable = ExecutableBox.Text ?? string.Empty;
        ViewModel.BuildPlatform = PlatformCombo.SelectedItem as string ?? "windows";
        ViewModel.BuildArchitecture = ArchitectureCombo.SelectedItem as string ?? "x64";
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var path = await WinUiFilePicker.PickFolderAsync(App.MainWindow, commitLabel: Loc.T("Wybierz folder"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ViewModel.SetFolderAsync(path);
        if (ViewModel.HasManifestForm)
        {
            PushFormFromViewModel();
        }

        RefreshUi();
    }

    private async void PickCover_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickSingleFileAsync(
            [".png", ".jpg", ".jpeg", ".webp", ".gif"],
            Loc.T("Obrazy"));
        if (path is null)
        {
            return;
        }

        if (!ViewModel.TrySetCoverFromFile(path, out var error))
        {
            ViewModel.Issues.Clear();
            ViewModel.Issues.Add(error ?? Loc.T("Nie udało się ustawić miniaturki."));
            ViewModel.StatusText = Loc.T("Nie udało się dodać miniaturki.");
        }

        PushFormFromViewModel();
        RefreshUi();
    }

    private async void PickHeader_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickSingleFileAsync(
            [".png", ".jpg", ".jpeg", ".webp", ".gif"],
            Loc.T("Obrazy"));
        if (path is null)
        {
            return;
        }

        if (!ViewModel.TrySetHeaderFromFile(path, out var error))
        {
            ViewModel.Issues.Clear();
            ViewModel.Issues.Add(error ?? Loc.T("Nie udało się ustawić okładki poziomej."));
            ViewModel.StatusText = Loc.T("Nie udało się dodać okładki poziomej.");
        }

        PushFormFromViewModel();
        RefreshUi();
    }

    private async void PickLogo_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickSingleFileAsync(
            [".png", ".jpg", ".jpeg", ".webp", ".gif"],
            Loc.T("Obrazy"));
        if (path is null)
        {
            return;
        }

        if (!ViewModel.TrySetLogoFromFile(path, out var error))
        {
            ViewModel.Issues.Clear();
            ViewModel.Issues.Add(error ?? Loc.T("Nie udało się ustawić logo."));
            ViewModel.StatusText = Loc.T("Nie udało się dodać logo.");
        }

        PushFormFromViewModel();
        RefreshUi();
    }

    private async void PickIntro_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickSingleFileAsync([".mp4", ".webm"], Loc.T("Wideo"));
        if (path is null)
        {
            return;
        }

        if (!ViewModel.TrySetIntroFromFile(path, out var error))
        {
            ViewModel.Issues.Clear();
            ViewModel.Issues.Add(error ?? Loc.T("Nie udało się ustawić intro."));
            ViewModel.StatusText = Loc.T("Nie udało się dodać intro.");
        }

        PushFormFromViewModel();
        RefreshUi();
    }

    private async void PickScreenshots_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var paths = await WinUiFilePicker.PickMultipleFilesAsync(
            App.MainWindow,
            [".png", ".jpg", ".jpeg", ".webp", ".gif"],
            commitLabel: Loc.T("Dodaj"));
        if (paths.Count == 0)
        {
            return;
        }

        if (!ViewModel.TryAddScreenshotsFromFiles(paths, out var error))
        {
            ViewModel.Issues.Clear();
            ViewModel.Issues.Add(error ?? Loc.T("Nie udało się dodać screenów."));
            ViewModel.StatusText = Loc.T("Nie udało się dodać screenów.");
        }

        PushFormFromViewModel();
        RefreshUi();
    }

    private void ClearCover_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearCover();
        PushFormFromViewModel();
        RefreshUi();
    }

    private void ClearHeader_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearHeader();
        PushFormFromViewModel();
        RefreshUi();
    }

    private void ClearLogo_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearLogo();
        PushFormFromViewModel();
        RefreshUi();
    }

    private void ClearIntro_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearIntro();
        PushFormFromViewModel();
        RefreshUi();
    }

    private void ClearScreenshots_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearScreenshots();
        PushFormFromViewModel();
        RefreshUi();
    }

    private async Task<string?> PickSingleFileAsync(IEnumerable<string> extensions, string commitLabel)
    {
        if (App.MainWindow is null)
        {
            return null;
        }

        return await WinUiFilePicker.PickSingleFileAsync(App.MainWindow, extensions, commitLabel);
    }

    private async void Prepare_Click(object sender, RoutedEventArgs e)
    {
        PullFormIntoViewModel();
        await ViewModel.PrepareCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private async void Publish_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PublishCommand.ExecuteAsync(null);
        RefreshUi();
    }
}
