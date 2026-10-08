using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        ViewModel = App.Services.GetRequiredService<HomeViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.NavigateRequested += OnNavigateRequested;
        ViewModel.OpenGameRequested += OnOpenGameRequested;
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HomeViewModel.CanPublish) or null)
            {
                ApplyPublishColumn();
            }

            if (e.PropertyName is nameof(HomeViewModel.Hero) or null)
            {
                UpdateHeroBackdrop();
            }
        };
        ApplyPublishColumn();
    }

    private void ApplyPublishColumn()
    {
        PublishColumn.Width = ViewModel.CanPublish ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.EnsureLoadedAsync();
        UpdateHeroBackdrop();
    }

    private void UpdateHeroBackdrop()
    {
        var url = ViewModel.Hero?.CoverImageUrl;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            ElementCompositionPreview.SetElementChildVisual(HeroBackdropHost, null);
            return;
        }

        var compositor = ElementCompositionPreview.GetElementVisual(HeroBackdropHost).Compositor;
        var surface = LoadedImageSurface.StartLoadFromUri(uri);
        var source = compositor.CreateSurfaceBrush(surface);
        source.Stretch = CompositionStretch.UniformToFill;

        var blur = new GaussianBlurEffect
        {
            Name = "Blur",
            BlurAmount = 36,
            BorderMode = EffectBorderMode.Hard,
            Source = new CompositionEffectSourceParameter("source")
        };
        var brush = compositor.CreateEffectFactory(blur).CreateBrush();
        brush.SetSourceParameter("source", source);

        var sprite = compositor.CreateSpriteVisual();
        sprite.Brush = brush;
        sprite.RelativeSizeAdjustment = Vector2.One;
        ElementCompositionPreview.SetElementChildVisual(HeroBackdropHost, sprite);
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenStoreCommand.Execute(null);

    private void OpenLibrary_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenLibraryCommand.Execute(null);

    private void OpenPublish_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenPublishCommand.Execute(null);

    private void OpenFriends_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenFriendsCommand.Execute(null);

    private void OpenHero_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenHeroCommand.Execute(null);

    private bool _sliding;

    private void OpenHero_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e) =>
        ViewModel.OpenHeroCommand.Execute(null);

    private void PreviousSlide_Click(object sender, RoutedEventArgs e) =>
        _ = SlideToAsync(-1, () => ViewModel.PreviousSlideCommand.Execute(null));

    private void NextSlide_Click(object sender, RoutedEventArgs e) =>
        _ = SlideToAsync(1, () => ViewModel.NextSlideCommand.Execute(null));

    private void Dot_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int index })
        {
            return;
        }

        var current = ViewModel.FeaturedDots.FirstOrDefault(d => d.IsActive)?.Index ?? 0;
        if (index == current)
        {
            return;
        }

        _ = SlideToAsync(index > current ? 1 : -1, () => ViewModel.SelectSlideCommand.Execute(index));
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
            var host = ElementCompositionPreview.GetElementVisual(HeroClip);
            host.Clip = host.Compositor.CreateInsetClip();

            var visual = ElementCompositionPreview.GetElementVisual(HeroCapsule);
            var compositor = visual.Compositor;
            var distance = (float)Math.Max(HeroClip.ActualWidth, 1);
            var ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0), new Vector2(0, 1));

            var exit = compositor.CreateVector3KeyFrameAnimation();
            exit.InsertKeyFrame(0, Vector3.Zero);
            exit.InsertKeyFrame(1, new Vector3(direction > 0 ? -distance : distance, 0, 0), ease);
            exit.Duration = TimeSpan.FromMilliseconds(180);
            visual.StartAnimation("Offset", exit);
            await Task.Delay(180);

            apply();

            var enter = compositor.CreateVector3KeyFrameAnimation();
            enter.InsertKeyFrame(0, new Vector3(direction > 0 ? distance : -distance, 0, 0));
            enter.InsertKeyFrame(1, Vector3.Zero, ease);
            enter.Duration = TimeSpan.FromMilliseconds(220);
            visual.StartAnimation("Offset", enter);
            await Task.Delay(220);
        }
        finally
        {
            _sliding = false;
        }
    }

    private void Rail_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is HomeGameCard item)
        {
            ViewModel.OpenGameCommand.Execute(item);
        }
    }

    private void Category_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is HomeCategoryItem item)
        {
            ViewModel.OpenCategoryCommand.Execute(item);
        }
    }

    private void OnNavigateRequested(string tag)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        if (tag == "Friends")
        {
            App.MainWindow.OpenSocialOverlay();
            return;
        }

        if (tag.StartsWith("Games:genre:", StringComparison.OrdinalIgnoreCase))
        {
            App.MainWindow.NavigateByTagPublic(tag);
            return;
        }

        var pageType = tag switch
        {
            "Games" => typeof(GamesPage),
            "Library" => typeof(LibraryPage),
            "Studio" => typeof(StudioPage),
            "Publish" => typeof(PublishPage),
            _ => typeof(HomePage)
        };

        App.MainWindow.NavigateTo(pageType, selectTag: tag);
    }

    private void OnOpenGameRequested(int gameId)
    {
        App.MainWindow?.NavigateTo(typeof(GameDetailsPage), parameter: new GameDetailsNavArgs(gameId, "Games"), selectTag: "Games");
    }
}
