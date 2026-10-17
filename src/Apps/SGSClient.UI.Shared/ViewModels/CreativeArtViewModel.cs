using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

public sealed partial class CreativeArtViewModel(ISgsApiService? api, string apiBaseUrl)
    : CreativeSectionViewModel(api, apiBaseUrl, GameContentKind.Art)
{
    [ObservableProperty] public partial bool IsLightboxOpen { get; set; }
    [ObservableProperty] public partial string SelectedTagsText { get; set; } = string.Empty;

    [RelayCommand]
    private void Open(CreativeItem? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedItem = item;
        SelectedTagsText = string.Join(", ", item.Tags);
        IsLightboxOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsLightboxOpen = false;
        SelectedItem = null;
    }

    [RelayCommand]
    private void Download()
    {
        if (SelectedItem is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedItem.FileUrl,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            StatusText = Loc.T("Nie udało się otworzyć pliku.");
        }
    }
}
