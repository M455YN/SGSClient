using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
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

    private bool _sliding;

    private void OpenHero_OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        (DataContext as HomeViewModel)?.OpenHeroCommand.Execute(null);

    private void PreviousSlide_OnClick(object? sender, RoutedEventArgs e) =>
        _ = SlideToAsync(-1, () => (DataContext as HomeViewModel)?.PreviousSlideCommand.Execute(null));

    private void NextSlide_OnClick(object? sender, RoutedEventArgs e) =>
        _ = SlideToAsync(1, () => (DataContext as HomeViewModel)?.NextSlideCommand.Execute(null));

    private void Dot_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm || sender is not Control { Tag: int index })
        {
            return;
        }

        var current = vm.FeaturedDots.FirstOrDefault(d => d.IsActive)?.Index ?? 0;
        if (index == current)
        {
            return;
        }

        _ = SlideToAsync(index > current ? 1 : -1, () => vm.SelectSlideCommand.Execute(index));
    }

    private async Task SlideToAsync(int direction, Action apply)
    {
        if (_sliding)
        {
            return;
        }

        _sliding = true;
        try
        {
            var distance = HeroClip.Bounds.Width;
            if (distance < 1)
            {
                distance = 480;
            }

            var exitTo = direction > 0 ? -distance : distance;
            await SlideAsync(0, exitTo, TimeSpan.FromMilliseconds(180));
            apply();
            await SlideAsync(-exitTo, 0, TimeSpan.FromMilliseconds(220));
        }
        finally
        {
            _sliding = false;
        }
    }

    private Task SlideAsync(double from, double to, TimeSpan duration)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(TranslateTransform.XProperty, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(TranslateTransform.XProperty, to) }
                }
            }
        };
        return animation.RunAsync((TranslateTransform)HeroCapsule.RenderTransform!);
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
