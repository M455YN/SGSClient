using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class HomeView : UserControl
{
    private bool _wired;

    public HomeView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            if (DataContext is not HomeViewModel vm)
            {
                return;
            }

            if (!_wired)
            {
                _wired = true;
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName is nameof(HomeViewModel.CanPublish) or null)
                    {
                        ApplyPublishColumn(vm.CanPublish);
                    }
                };
            }

            ApplyPublishColumn(vm.CanPublish);
            await vm.EnsureLoadedAsync();
        };
    }

    private void ApplyPublishColumn(bool canPublish)
    {
        QuickActionsGrid.ColumnDefinitions[3].Width =
            canPublish ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    private void OpenStore_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as HomeViewModel)?.OpenStoreCommand.Execute(null);

    private void OpenLibrary_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as HomeViewModel)?.OpenLibraryCommand.Execute(null);

    private void OpenPublish_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as HomeViewModel)?.OpenPublishCommand.Execute(null);

    private void OpenFriends_OnClick(object? sender, RoutedEventArgs e) =>
        (DataContext as HomeViewModel)?.OpenFriendsCommand.Execute(null);

    private void OpenHero_OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as HomeViewModel)?.OpenHeroCommand.Execute(null);

    private void Dot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm && sender is Control { Tag: int index })
        {
            vm.SelectSlideCommand.Execute(index);
        }
    }

    private void Card_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is HomeViewModel vm && (sender as Control)?.Tag is HomeGameCard item)
        {
            vm.OpenGameCommand.Execute(item);
        }
    }

    private void Category_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is HomeViewModel vm && (sender as Control)?.Tag is HomeCategoryItem item)
        {
            vm.OpenCategoryCommand.Execute(item);
        }
    }
}
