using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using SGSClient.Core.Localization;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>Achievements tab body shared by the Creator and Admin panels.</summary>
public partial class AchievementsEditor : UserControl
{
    public AchievementsEditor()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Vm?.SetGreyscaleHandler(GreyscaleIcon.CreateAsync);
    }

    private AchievementsEditorViewModel? Vm => DataContext as AchievementsEditorViewModel;

    // ---- table ----------------------------------------------------------------------------

    private async void New_OnClick(object? sender, RoutedEventArgs e) => await OpenEditorAsync(null);

    private async void Edit_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: StudioAchievementItem item })
        {
            await OpenEditorAsync(item);
        }
    }

    private async void MoveUp_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && sender is Control { Tag: StudioAchievementItem item })
        {
            await vm.MoveUpCommand.ExecuteAsync(item);
        }
    }

    private async void MoveDown_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && sender is Control { Tag: StudioAchievementItem item })
        {
            await vm.MoveDownCommand.ExecuteAsync(item);
        }
    }

    private async void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || sender is not Control { Tag: StudioAchievementItem item })
        {
            return;
        }

        var confirm = new FAContentDialog
        {
            Title = Loc.T("Usunąć osiągnięcie?"),
            Content = Loc.F("„{0}” zniknie z listy gry. Gracze, którzy je zdobyli, zachowają je, a ponowne dodanie tego samego API name je przywróci.", item.DisplayTitle),
            PrimaryButtonText = Loc.T("Usuń"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = FAContentDialogButton.Close
        };
        if (await confirm.ShowAsync(TopLevel.GetTopLevel(this)) == FAContentDialogResult.Primary)
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

        var dialog = new FAContentDialog
        {
            Title = item is null ? Loc.T("Nowe osiągnięcie") : Loc.T("Edytuj osiągnięcie"),
            Content = new AchievementEditDialog { DataContext = draft, Editor = vm },
            PrimaryButtonText = Loc.T("Zapisz"),
            CloseButtonText = Loc.T("Anuluj"),
            DefaultButton = FAContentDialogButton.Primary
        };
        dialog.Resources["ContentDialogMaxWidth"] = 900d;
        dialog.Bind(FAContentDialog.IsPrimaryButtonEnabledProperty, new Binding(nameof(StudioAchievementItem.CanSave)) { Source = draft });

        // Saves before closing; a failed save keeps the dialog open with the reason shown in it.
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                args.Cancel = !await vm.CommitEditAsync();
            }
            finally
            {
                deferral.Complete();
            }
        };

        try
        {
            await dialog.ShowAsync(TopLevel.GetTopLevel(this));
        }
        finally
        {
            vm.EndEdit();
        }
    }

    // ---- stats ----------------------------------------------------------------------------

    private void ToggleStat_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && sender is Control { Tag: StudioStatItem item })
        {
            vm.ToggleEditCommand.Execute(item);
        }
    }

    private void RemoveStat_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is { } vm && sender is Control { Tag: StudioStatItem item })
        {
            vm.RemoveStatCommand.Execute(item);
        }
    }
}
