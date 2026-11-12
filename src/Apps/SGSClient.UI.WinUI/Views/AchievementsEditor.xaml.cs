using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

/// <summary>Achievements tab body shared by the Creator and Admin pages.</summary>
public sealed partial class AchievementsEditor : UserControl
{
    public AchievementsEditor()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Vm?.SetGreyscaleHandler(GreyscaleIcon.CreateAsync);
    }

    private AchievementsEditorViewModel? Vm => DataContext as AchievementsEditorViewModel;

    // ---- table ----------------------------------------------------------------------------

    private async void New_Click(object sender, RoutedEventArgs e) => await OpenEditorAsync(null);

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioAchievementItem item })
        {
            await OpenEditorAsync(item);
        }
    }

    private async void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && sender is FrameworkElement { Tag: StudioAchievementItem item })
        {
            await vm.MoveUpCommand.ExecuteAsync(item);
        }
    }

    private async void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && sender is FrameworkElement { Tag: StudioAchievementItem item })
        {
            await vm.MoveDownCommand.ExecuteAsync(item);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || sender is not FrameworkElement { Tag: StudioAchievementItem item })
        {
            return;
        }

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Loc.T("Usunąć osiągnięcie?"),
            Content = Loc.F("„{0}” zniknie z listy gry. Gracze, którzy je zdobyli, zachowają je, a ponowne dodanie tego samego API name je przywróci.", item.DisplayTitle),
            PrimaryButtonText = Loc.T("Usuń"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            await vm.DeleteAsync(item);
        }
    }

    // ---- dialog ---------------------------------------------------------------------------

    private async Task OpenEditorAsync(StudioAchievementItem? item)
    {
        if (Vm is not { } vm || vm.BeginEdit(item) is not { } draft)
        {
            return;
        }

        EditDialog.Title = item is null ? Loc.T("Nowe osiągnięcie") : Loc.T("Edytuj osiągnięcie");
        EditDialog.DataContext = draft;
        EditDialog.XamlRoot = XamlRoot;
        try
        {
            await EditDialog.ShowAsync();
        }
        finally
        {
            vm.EndEdit();
            EditDialog.DataContext = null;
        }
    }

    /// <summary>Saves before closing; a failed save keeps the dialog open with the reason in the InfoBar.</summary>
    private async void EditDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            args.Cancel = Vm is not { } vm || !await vm.CommitEditAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void PickIcon_Click(object sender, RoutedEventArgs e) => await (Vm?.PickDraftIconAsync(locked: false) ?? Task.CompletedTask);

    private async void PickLockedIcon_Click(object sender, RoutedEventArgs e) => await (Vm?.PickDraftIconAsync(locked: true) ?? Task.CompletedTask);

    private async void Greyscale_Click(object sender, RoutedEventArgs e) => await (Vm?.MakeGreyscaleLockedIconAsync() ?? Task.CompletedTask);

    private void ClearIcon_Click(object sender, RoutedEventArgs e) => Vm?.ClearDraftIcon(locked: false);

    private void ClearLockedIcon_Click(object sender, RoutedEventArgs e) => Vm?.ClearDraftIcon(locked: true);

    private async void TestUnlock_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && EditDialog.DataContext is StudioAchievementItem draft)
        {
            await vm.TestUnlockCommand.ExecuteAsync(draft);
        }
    }

    // ---- stats ----------------------------------------------------------------------------

    private async void NewStat_Click(object sender, RoutedEventArgs e) => await OpenStatEditorAsync(null);

    private async void EditStat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StudioStatItem item })
        {
            await OpenStatEditorAsync(item);
        }
    }

    private async void DeleteStat_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || sender is not FrameworkElement { Tag: StudioStatItem item })
        {
            return;
        }

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Loc.T("Usunąć statystykę?"),
            Content = Loc.F("„{0}” zniknie z listy gry. Wartości graczy zostają, a ponowne dodanie tego samego API name je przywróci.", item.EditKey),
            PrimaryButtonText = Loc.T("Usuń"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = ContentDialogButton.Close
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            await vm.DeleteStatAsync(item);
        }
    }

    private async Task OpenStatEditorAsync(StudioStatItem? item)
    {
        if (Vm is not { } vm || vm.BeginEditStat(item) is not { } draft)
        {
            return;
        }

        StatDialog.Title = item is null ? Loc.T("Nowa statystyka") : Loc.T("Edytuj statystykę");
        StatDialog.DataContext = draft;
        StatDialog.XamlRoot = XamlRoot;
        try
        {
            await StatDialog.ShowAsync();
        }
        finally
        {
            vm.EndEditStat();
            StatDialog.DataContext = null;
        }
    }

    private async void StatDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            args.Cancel = Vm is not { } vm || !await vm.CommitStatEditAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }
}
