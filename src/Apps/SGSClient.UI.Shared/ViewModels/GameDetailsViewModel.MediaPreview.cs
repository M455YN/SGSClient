using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Install;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;
using SGSClient.Core.Playtime;

namespace SGSClient.UI.ViewModels;

public partial class GameDetailsViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewCounterText))]
    [NotifyPropertyChangedFor(nameof(CanPreviewPrevious))]
    [NotifyPropertyChangedFor(nameof(CanPreviewNext))]
    [NotifyPropertyChangedFor(nameof(PreviewHintText))]
    public partial bool IsMediaPreviewOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewCounterText))]
    [NotifyPropertyChangedFor(nameof(CanPreviewPrevious))]
    [NotifyPropertyChangedFor(nameof(CanPreviewNext))]
    [NotifyPropertyChangedFor(nameof(PreviewHintText))]
    public partial int MediaPreviewIndex { get; set; }

    [ObservableProperty]
    public partial string? MediaPreviewUrl { get; set; }

    [ObservableProperty]
    public partial string MediaPreviewLabel { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewHintText))]
    public partial bool MediaPreviewIsVideo { get; set; }

    public string PreviewCounterText
    {
        get
        {
            var count = PreviewableThumbs.Count;
            return count == 0 ? string.Empty : $"{MediaPreviewIndex + 1} / {count}";
        }
    }

    public bool CanPreviewPrevious => IsMediaPreviewOpen && MediaPreviewIndex > 0;

    public bool CanPreviewNext =>
        IsMediaPreviewOpen && MediaPreviewIndex < PreviewableThumbs.Count - 1;

    public string PreviewHintText =>
        MediaPreviewIsVideo
            ? (PreviewableThumbs.Count > 1
                ? Loc.T("Odtwarzanie trailera · strzałki przełączają multimedia")
                : Loc.T("Odtwarzanie trailera · kliknij ✕, żeby zamknąć"))
            : PreviewableThumbs.Count > 1
                ? Loc.T("Kliknij strzałkę lub tło, żeby zobaczyć kolejne zdjęcie")
                : Loc.T("Kliknij tło lub ✕, żeby zamknąć");

    private IReadOnlyList<MediaThumbItem> PreviewableThumbs =>
        MediaThumbs.Where(t => t.HasMedia).ToList();

    [RelayCommand]
    private void OpenMediaPreview(MediaThumbItem? thumb)
    {
        if (thumb is null || !thumb.HasMedia)
        {
            return;
        }

        var items = PreviewableThumbs;
        var index = items.ToList().FindIndex(t =>
            string.Equals(t.ImageUrl, thumb.ImageUrl, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        ShowPreviewAt(index);
    }

    [RelayCommand]
    private void OpenCoverPreview()
    {
        var items = PreviewableThumbs;
        if (items.Count == 0)
        {
            return;
        }

        var index = items.ToList().FindIndex(t => t.IsImage);
        ShowPreviewAt(index >= 0 ? index : 0);
    }

    [RelayCommand]
    private void OpenIntroPreview()
    {
        var items = PreviewableThumbs;
        var index = items.ToList().FindIndex(t => t.IsVideo);
        if (index < 0)
        {
            return;
        }

        ShowPreviewAt(index);
    }

    [RelayCommand]
    private void CloseMediaPreview()
    {
        IsMediaPreviewOpen = false;
        MediaPreviewIsVideo = false;
        MediaPreviewUrl = null;
    }

    [RelayCommand]
    private void PreviewPrevious()
    {
        if (!CanPreviewPrevious)
        {
            return;
        }

        ShowPreviewAt(MediaPreviewIndex - 1);
    }

    [RelayCommand]
    private void PreviewNext()
    {
        if (!CanPreviewNext)
        {
            return;
        }

        ShowPreviewAt(MediaPreviewIndex + 1);
    }

    private void ShowPreviewAt(int index)
    {
        var items = PreviewableThumbs;
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        var item = items[index];
        MediaPreviewIndex = index;
        MediaPreviewUrl = item.ImageUrl;
        MediaPreviewLabel = item.Label;
        MediaPreviewIsVideo = item.IsVideo;
        IsMediaPreviewOpen = true;
        OnPropertyChanged(nameof(PreviewCounterText));
        OnPropertyChanged(nameof(CanPreviewPrevious));
        OnPropertyChanged(nameof(CanPreviewNext));
        OnPropertyChanged(nameof(PreviewHintText));
    }
}
