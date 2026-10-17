using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.WinUI.Views;

public sealed partial class CreativePage : Page
{
    private bool _syncing;

    public CreativeViewModel ViewModel { get; }

    public CreativePage()
    {
        ViewModel = App.Services.GetRequiredService<CreativeViewModel>();
        InitializeComponent();
        ArtSection.ViewModel = ViewModel.Art;
        MusicSection.ViewModel = ViewModel.Music;
        StoriesSection.ViewModel = ViewModel.Stories;
        ViewModel.PropertyChanged += (_, _) => RefreshUi();
        RefreshUi();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private void RefreshUi()
    {
        _syncing = true;
        ArtSection.Visibility = ViewModel.ShowArt ? Visibility.Visible : Visibility.Collapsed;
        MusicSection.Visibility = ViewModel.ShowMusic ? Visibility.Visible : Visibility.Collapsed;
        StoriesSection.Visibility = ViewModel.ShowStories ? Visibility.Visible : Visibility.Collapsed;
        var selected = SectionBar.Items.FirstOrDefault(i => (string?)i.Tag == ViewModel.SelectedSection);
        if (selected is not null && !ReferenceEquals(SectionBar.SelectedItem, selected))
        {
            SectionBar.SelectedItem = selected;
        }

        _syncing = false;
    }

    private void Section_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (!_syncing && sender.SelectedItem?.Tag is string section)
        {
            ViewModel.SelectedSection = section;
        }
    }
}
