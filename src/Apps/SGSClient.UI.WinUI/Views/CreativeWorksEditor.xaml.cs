using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

/// <summary>List + editor of artworks, tracks or stories; shared by the Creator and Admin pages.</summary>
public sealed partial class CreativeWorksEditor : UserControl
{
    public CreativeWorksEditor()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is CreativeWorksViewModel vm)
            {
                vm.SetPickCoverHandler(PickCoverAsync);
                vm.SetConfirmDeleteHandler(ConfirmDeleteAsync);
            }
        };
    }

    private static async Task<string?> PickCoverAsync() =>
        App.MainWindow is null
            ? null
            : await WinUiFilePicker.PickSingleFileAsync(
                App.MainWindow, [".png", ".jpg", ".jpeg", ".webp", ".gif"], commitLabel: Loc.T("Wybierz"));

    private async Task<bool> ConfirmDeleteAsync(string title)
    {
        var dialog = new ContentDialog
        {
            Title = Loc.T("Usunąć pozycję?"),
            Content = Loc.F("„{0}” zniknie ze sklepu. Tej operacji nie da się cofnąć.", title),
            PrimaryButtonText = Loc.T("Usuń"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
