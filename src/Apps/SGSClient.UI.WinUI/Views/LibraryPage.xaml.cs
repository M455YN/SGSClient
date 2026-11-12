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

public sealed partial class LibraryPage : Page, ILoadingPage
{
    private readonly Dictionary<FrameworkElement, CaptionBlurState> _captionBlurs = new();

    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
        InitializeComponent();
        ViewModel.SetOpenPropertiesHandler(OpenProperties);
        ViewModel.SetOpenDetailsHandler(OpenDetails);
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
    }

    public Task Ready { get; private set; } = Task.CompletedTask;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Ready = LoadForNavigationAsync(e);
    }

    private async Task LoadForNavigationAsync(NavigationEventArgs e)
    {
        await ViewModel.LoadAsync();
        RefreshUi();
        App.MainWindow?.RefreshSidebar();
    }

    private void RefreshUi()
    {
        SidebarStatus.Text = ViewModel.StatusText;
        EmptyText.Text = ViewModel.StatusText;
        EmptyText.Visibility = ViewModel.ShowEmpty ? Visibility.Visible : Visibility.Collapsed;
        HomeScroll.Visibility = ViewModel.ShowHome && ViewModel.HasGames ? Visibility.Visible : Visibility.Collapsed;

        SidebarList.ItemsSource = ViewModel.FilteredGames;
        RecentList.ItemsSource = ViewModel.RecentGames;
        AllGamesList.ItemsSource = ViewModel.AllGames;
        RecentPanel.Visibility = ViewModel.ShowRecent ? Visibility.Visible : Visibility.Collapsed;
        AllGamesHeader.Text = ViewModel.AllGamesHeader;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
        RefreshUi();
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectHomeCommand.Execute(null);
        RefreshUi();
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        ViewModel.SearchText = sender.Text ?? string.Empty;
        SidebarList.ItemsSource = null;
        SidebarList.ItemsSource = ViewModel.FilteredGames;
    }

    private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SidebarList.SelectedItem is LibraryGameItem item)
        {
            ViewModel.SelectGameCommand.Execute(item);
        }
    }

    private void Poster_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LibraryGameItem item)
        {
            ViewModel.SelectGameCommand.Execute(item);
        }
    }

    private void OpenDetails(int gameId)
    {
        App.MainWindow?.NavigateTo(
            typeof(GameDetailsPage),
            parameter: new GameDetailsNavArgs(gameId, "Library"),
            selectTag: "Library");
    }

    private void PosterCaption_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement host)
        {
            return;
        }

        if (!_captionBlurs.ContainsKey(host))
        {
            host.RegisterPropertyChangedCallback(FrameworkElement.TagProperty, OnCaptionTagChanged);
            _captionBlurs[host] = new CaptionBlurState();
        }

        UpdatePosterCaptionBlur(host);
    }

    private void PosterCaption_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not FrameworkElement caption)
        {
            return;
        }

        // SizeChanged is on the caption Grid; blur lives on its first child Border.
        if (VisualTreeHelper.GetChildrenCount(caption) > 0
            && VisualTreeHelper.GetChild(caption, 0) is FrameworkElement host)
        {
            UpdatePosterCaptionBlur(host);
        }
    }

    private void PosterCaption_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement host || !_captionBlurs.Remove(host, out var state))
        {
            return;
        }

        state.Dispose(host);
    }

    private void OnCaptionTagChanged(DependencyObject sender, DependencyProperty dp) =>
        UpdatePosterCaptionBlur(sender as FrameworkElement);

    private void UpdatePosterCaptionBlur(FrameworkElement? host)
    {
        if (host is null || !_captionBlurs.TryGetValue(host, out var state))
        {
            return;
        }

        var caption = VisualTreeHelper.GetParent(host) as FrameworkElement;
        var card = caption is null ? null : VisualTreeHelper.GetParent(caption) as FrameworkElement;
        if (caption is null
            || card is null
            || caption.ActualWidth < 1
            || caption.ActualHeight < 1
            || card.ActualWidth < 1
            || card.ActualHeight < 1)
        {
            return;
        }

        var compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;
        if (state.Sprite is null || state.Source is null)
        {
            var blur = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 22,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("source")
            };
            var effect = compositor.CreateEffectFactory(blur).CreateBrush();
            var source = compositor.CreateSurfaceBrush();
            source.Stretch = CompositionStretch.UniformToFill;
            source.HorizontalAlignmentRatio = 0.5f;
            source.VerticalAlignmentRatio = 0.5f;
            effect.SetSourceParameter("source", source);

            var sprite = compositor.CreateSpriteVisual();
            sprite.Brush = effect;
            state.Source = source;
            state.Effect = effect;
            state.Sprite = sprite;
            ElementCompositionPreview.SetElementChildVisual(host, sprite);
        }

        var url = host.Tag as string;
        if (!string.Equals(state.Url, url, StringComparison.Ordinal))
        {
            state.Url = url;
            state.Surface?.Dispose();
            state.Surface = null;
            if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                state.Surface = LoadedImageSurface.StartLoadFromUri(uri);
                state.Source.Surface = state.Surface;
            }
            else
            {
                state.Source.Surface = null;
            }
        }

        var cardWidth = (float)card.ActualWidth;
        var cardHeight = (float)card.ActualHeight;
        var captionHeight = (float)caption.ActualHeight;
        state.Sprite.Size = new Vector2(cardWidth, cardHeight);
        state.Sprite.Offset = new Vector3(0, captionHeight - cardHeight, 0);

        var clip = compositor.CreateRectangleClip();
        clip.Right = (float)caption.ActualWidth;
        clip.Bottom = captionHeight;
        clip.BottomLeftRadius = new Vector2(8, 8);
        clip.BottomRightRadius = new Vector2(8, 8);
        ElementCompositionPreview.GetElementVisual(host).Clip = clip;
    }

    private sealed class CaptionBlurState
    {
        public string? Url { get; set; }
        public CompositionSurfaceBrush? Source { get; set; }
        public CompositionEffectBrush? Effect { get; set; }
        public SpriteVisual? Sprite { get; set; }
        public LoadedImageSurface? Surface { get; set; }

        public void Dispose(FrameworkElement host)
        {
            ElementCompositionPreview.SetElementChildVisual(host, null);
            Surface?.Dispose();
            Sprite?.Dispose();
            Effect?.Dispose();
            Source?.Dispose();
        }
    }

    private async void OpenProperties(LibraryGameItem item)
    {
        var dialog = new GamePropertiesDialog();
        await dialog.ShowForGameAsync(item.GameId, item.BuildId, item.Title, XamlRoot);
        await ViewModel.LoadAsync();
        RefreshUi();
        App.MainWindow?.RefreshSidebar();
    }
}
