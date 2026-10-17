using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>List + editor of artworks, tracks or stories; shared by the Creator and Admin panels.</summary>
public partial class CreativeWorksEditor : UserControl
{
    private static readonly FilePickerFileType ImageFiles = new(Loc.T("Obrazy"))
    {
        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif"],
        MimeTypes = ["image/*"]
    };

    public CreativeWorksEditor()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is CreativeWorksViewModel vm)
            {
                vm.SetPickCoverHandler(() => AvaloniaStoragePicker.PickSingleFileAsync(
                    TopLevel.GetTopLevel(this), ImageFiles, Loc.T("Wybierz obraz")));
                vm.SetConfirmDeleteHandler(ConfirmDeleteAsync);
            }
        };
    }

    private async Task<bool> ConfirmDeleteAsync(string title)
    {
        var dialog = new FAContentDialog
        {
            Title = Loc.T("Usunąć pozycję?"),
            Content = Loc.F("„{0}” zniknie ze sklepu. Tej operacji nie da się cofnąć.", title),
            PrimaryButtonText = Loc.T("Usuń"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = FAContentDialogButton.Close
        };
        return await dialog.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary;
    }
}
