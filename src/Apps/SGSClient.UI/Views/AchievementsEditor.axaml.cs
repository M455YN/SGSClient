using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>Achievements tab body shared by the Creator and Admin panels.</summary>
public partial class AchievementsEditor : UserControl
{
    public AchievementsEditor() => InitializeComponent();

    private void MoveUp_OnClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.MoveUpCommand.Execute(item));

    private void MoveDown_OnClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.MoveDownCommand.Execute(item));

    private void Remove_OnClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.RemoveCommand.Execute(item));

    private void ClearIcon_OnClick(object? sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.ClearIconCommand.Execute(item));

    private async void TestUnlock_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AchievementsEditorViewModel vm && sender is Control { Tag: StudioAchievementItem item })
        {
            await vm.TestUnlockCommand.ExecuteAsync(item);
        }
    }

    private async void PickIcon_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AchievementsEditorViewModel vm && sender is Control { Tag: StudioAchievementItem item })
        {
            await vm.PickIconCommand.ExecuteAsync(item);
        }
    }

    private void Run(object? sender, Action<AchievementsEditorViewModel, StudioAchievementItem> action)
    {
        if (DataContext is AchievementsEditorViewModel vm && sender is Control { Tag: StudioAchievementItem item })
        {
            action(vm, item);
        }
    }
}
