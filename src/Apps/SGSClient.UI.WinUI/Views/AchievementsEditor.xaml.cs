using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

/// <summary>Achievements tab body shared by the Creator and Admin pages.</summary>
public sealed partial class AchievementsEditor : UserControl
{
    public AchievementsEditor() => InitializeComponent();

    private void MoveUp_Click(object sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.MoveUpCommand.Execute(item));

    private void MoveDown_Click(object sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.MoveDownCommand.Execute(item));

    private void Remove_Click(object sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.RemoveCommand.Execute(item));

    private void ClearIcon_Click(object sender, RoutedEventArgs e) => Run(sender, (vm, item) => vm.ClearIconCommand.Execute(item));

    private async void TestUnlock_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AchievementsEditorViewModel vm && sender is FrameworkElement { Tag: StudioAchievementItem item })
        {
            await vm.TestUnlockCommand.ExecuteAsync(item);
        }
    }

    private async void PickIcon_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AchievementsEditorViewModel vm && sender is FrameworkElement { Tag: StudioAchievementItem item })
        {
            await vm.PickIconCommand.ExecuteAsync(item);
        }
    }

    private void Run(object sender, Action<AchievementsEditorViewModel, StudioAchievementItem> action)
    {
        if (DataContext is AchievementsEditorViewModel vm && sender is FrameworkElement { Tag: StudioAchievementItem item })
        {
            action(vm, item);
        }
    }
}
