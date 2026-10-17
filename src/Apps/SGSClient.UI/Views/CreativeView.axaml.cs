using Avalonia.Controls;
using Avalonia.Interactivity;
using SGSClient.UI.ViewModels;

namespace SGSClient.UI.Views;

public partial class CreativeView : UserControl
{
    public CreativeView()
    {
        InitializeComponent();
        AttachedToVisualTree += async (_, _) =>
        {
            if (DataContext is CreativeViewModel vm)
            {
                SyncRadios(vm);
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(CreativeViewModel.SelectedSection))
                    {
                        SyncRadios(vm);
                    }
                };
                await vm.LoadAsync();
            }
        };
    }

    private void SyncRadios(CreativeViewModel vm)
    {
        foreach (var radio in SectionPanel.Children.OfType<RadioButton>())
        {
            radio.IsChecked = (string?)radio.Tag == vm.SelectedSection;
        }

        ArtSection.IsVisible = vm.ShowArt;
        MusicSection.IsVisible = vm.ShowMusic;
        StoriesSection.IsVisible = vm.ShowStories;
    }

    private void Section_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreativeViewModel vm && sender is RadioButton { Tag: string section })
        {
            vm.SelectedSection = section;
        }
    }
}
