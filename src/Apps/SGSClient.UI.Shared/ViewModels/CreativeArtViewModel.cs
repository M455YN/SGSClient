using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.DTOs;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

public sealed partial class CreativeArtViewModel : CreativeSectionViewModel
{
    private static readonly string AllCategories = Loc.T("Wszystkie");

    public CreativeArtViewModel(ISgsApiService? api, string apiBaseUrl)
        : base(api, apiBaseUrl)
    {
        Items.CollectionChanged += (_, _) => RebuildCategories();
        Comments = new CreativeCommentsViewModel(api, "art", apiBaseUrl);
    }

    public CreativeCommentsViewModel Comments { get; }

    protected override async Task<IEnumerable<CreativeItem>> FetchAsync(CreativeQuery query) =>
        (await Api!.GetArtworksAsync(query)).Select(x => CreativeItem.FromArtwork(x, ApiBaseUrl));

    /// <summary>Items after the category chip filter; the gallery binds to this.</summary>
    public ObservableCollection<CreativeItem> VisibleItems { get; } = [];

    /// <summary>"Wszystkie" followed by the most used tags, like the category chips of a gallery site.</summary>
    public ObservableCollection<string> Categories { get; } = [];

    [ObservableProperty] public partial string SelectedCategory { get; set; } = AllCategories;

    /// <summary>True while the artwork detail page is shown instead of the gallery.</summary>
    [ObservableProperty] public partial bool IsDetailOpen { get; set; }

    public bool ShowGallery => !IsDetailOpen;

    partial void OnIsDetailOpenChanged(bool value) => OnPropertyChanged(nameof(ShowGallery));

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    private void RebuildCategories()
    {
        var current = SelectedCategory;
        var top = Items
            .SelectMany(i => i.Tags)
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .Take(14)
            .ToList();
        Categories.Clear();
        Categories.Add(AllCategories);
        foreach (var tag in top)
        {
            Categories.Add(tag);
        }

        if (!Categories.Contains(current))
        {
            SelectedCategory = AllCategories;
            return;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        VisibleItems.Clear();
        foreach (var item in Items.Where(i => SelectedCategory == AllCategories || i.Tags.Contains(SelectedCategory)))
        {
            VisibleItems.Add(item);
        }
    }

    [RelayCommand]
    private void SelectCategory(string? category)
    {
        if (!string.IsNullOrEmpty(category))
        {
            SelectedCategory = category;
        }
    }

    [RelayCommand]
    private void Open(CreativeItem? item)
    {
        if (item is null)
        {
            return;
        }

        SelectedItem = item;
        IsDetailOpen = true;
        _ = Comments.LoadAsync(item.Id);
    }

    [RelayCommand]
    private void Close()
    {
        IsDetailOpen = false;
        SelectedItem = null;
        Comments.Clear();
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
            Process.Start(new ProcessStartInfo { FileName = SelectedItem.FileUrl, UseShellExecute = true });
        }
        catch (Exception)
        {
            StatusText = Loc.T("Nie udało się otworzyć pliku.");
        }
    }
}
