using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

/// <summary>Body of the add / edit achievement dialog. <see cref="Editor"/> runs the icon and test actions.</summary>
public partial class AchievementEditDialog : UserControl
{
    public AchievementEditDialog() => InitializeComponent();

    public AchievementsEditorViewModel? Editor { get; init; }

    private async void PickIcon_OnClick(object? sender, RoutedEventArgs e) =>
        await (Editor?.PickDraftIconAsync(locked: false) ?? Task.CompletedTask);

    private async void PickLockedIcon_OnClick(object? sender, RoutedEventArgs e) =>
        await (Editor?.PickDraftIconAsync(locked: true) ?? Task.CompletedTask);

    private async void Greyscale_OnClick(object? sender, RoutedEventArgs e) =>
        await (Editor?.MakeGreyscaleLockedIconAsync() ?? Task.CompletedTask);

    private void ClearIcon_OnClick(object? sender, RoutedEventArgs e) => Editor?.ClearDraftIcon(locked: false);

    private void ClearLockedIcon_OnClick(object? sender, RoutedEventArgs e) => Editor?.ClearDraftIcon(locked: true);

    private async void TestUnlock_OnClick(object? sender, RoutedEventArgs e)
    {
        if (Editor is { } editor && DataContext is StudioAchievementItem draft)
        {
            await editor.TestUnlockCommand.ExecuteAsync(draft);
        }
    }
}
