using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Models;

namespace SGSClient.UI.WinUI.ViewModels;

public partial class GameDetailsViewModel : ObservableObject
{
    private readonly ISgsApiService? _api;
    private readonly IServiceScopeFactory? _scopes;

    public GameDetailsViewModel(ISgsApiService? api, IServiceScopeFactory? scopes)
    {
        _api = api;
        _scopes = scopes;
        StatusText = "Ładowanie…";
    }

    public ObservableCollection<string> Badges { get; } = [];
    public ObservableCollection<MediaThumbItem> MediaThumbs { get; } = [];
    public ObservableCollection<BuildRowItem> Builds { get; } = [];

    [ObservableProperty]
    public partial int GameId { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Symbol { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TileLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Tagline { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HardwareRequirements { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OtherInformation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlatformsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedTab { get; set; } = "Details";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasContent { get; set; }

    [ObservableProperty]
    public partial bool InLibrary { get; set; }

    [ObservableProperty]
    public partial bool CanAdd { get; set; }

    [ObservableProperty]
    public partial string PrimaryActionLabel { get; set; } = "Dodaj do biblioteki";

    [ObservableProperty]
    public partial bool ShowHardware { get; set; }

    [ObservableProperty]
    public partial bool ShowOther { get; set; }

    [ObservableProperty]
    public partial bool ShowBuilds { get; set; }

    public bool ShowDetailsTab => SelectedTab == "Details";
    public bool ShowReviewsTab => SelectedTab == "Reviews";
    public bool ShowMoreTab => SelectedTab == "More";

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(ShowDetailsTab));
        OnPropertyChanged(nameof(ShowReviewsTab));
        OnPropertyChanged(nameof(ShowMoreTab));
    }

    public async Task LoadAsync(int gameId)
    {
        GameId = gameId;
        if (_api is null)
        {
            StatusText = "Brak klienta API.";
            HasContent = false;
            return;
        }

        IsBusy = true;
        HasContent = false;
        StatusText = "Ładowanie szczegółów…";
        try
        {
            var game = await _api.GetGameAsync(gameId);
            if (game is null)
            {
                StatusText = "Nie znaleziono gry.";
                return;
            }

            ApplyGame(game);
            InLibrary = await IsInLibraryAsync(game.Id);
            UpdateActionState(game);
            HasContent = true;
            StatusText = string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = $"Błąd: {ex.Message}";
            HasContent = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
        {
            SelectedTab = tab;
        }
    }

    [RelayCommand]
    private async Task AddToLibraryAsync()
    {
        if (!CanAdd || _scopes is null || GameId <= 0)
        {
            return;
        }

        var preferred = Builds.FirstOrDefault(b => b.IsPreferred) ?? Builds.FirstOrDefault();
        if (preferred is null || preferred.BuildId <= 0)
        {
            StatusText = "Brak builda dla tej platformy.";
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            await library.UpsertAsync(new GameLibraryItem
            {
                GameId = GameId,
                BuildId = preferred.BuildId,
                Symbol = Symbol,
                Title = Title,
                Platform = preferred.Platform,
                Architecture = preferred.Architecture,
                InstallState = InstallState.NotInstalled,
                InstalledVersion = preferred.Version,
                ExecutablePath = null,
                InstallPath = null
            });

            InLibrary = true;
            CanAdd = false;
            PrimaryActionLabel = "W bibliotece";
            StatusText = "Dodano do biblioteki.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się dodać: {ex.Message}";
        }
    }

    private void ApplyGame(GameDto game)
    {
        Title = game.Title;
        Symbol = game.Symbol;
        TileLabel = string.IsNullOrWhiteSpace(game.Symbol)
            ? game.Title[..Math.Min(2, game.Title.Length)].ToUpperInvariant()
            : game.Symbol[..Math.Min(3, game.Symbol.Length)].ToUpperInvariant();
        Tagline = string.IsNullOrWhiteSpace(game.Description)
            ? "Poznaj szczegóły tej gry."
            : game.Description.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() is { Length: > 0 } first
                ? first + "."
                : game.Description!;
        Description = string.IsNullOrWhiteSpace(game.Description) ? "Brak opisu." : game.Description!;
        HardwareRequirements = game.HardwareRequirements ?? string.Empty;
        OtherInformation = game.OtherInformation ?? string.Empty;
        ShowHardware = HardwareRequirements.Length > 0;
        ShowOther = OtherInformation.Length > 0;

        Badges.Clear();
        if (game.FeaturedP)
        {
            Badges.Add("Wyróżnione");
        }

        if (!string.IsNullOrWhiteSpace(game.GenreName))
        {
            Badges.Add(game.GenreName);
        }

        if (!string.IsNullOrWhiteSpace(game.EngineName))
        {
            Badges.Add(game.EngineName);
        }

        Badges.Add("Sklep SGS");
        foreach (var platform in game.Builds
                     .Select(b => FormatPlatform(b.Platform, b.Architecture))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Badges.Add(platform);
        }

        PlatformsText = game.Builds.Count == 0
            ? "Brak buildów"
            : string.Join(" · ", game.Builds
                .Select(b => FormatPlatform(b.Platform, b.Architecture))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        MediaThumbs.Clear();
        if (!string.IsNullOrWhiteSpace(game.CoverUrl))
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Okładka", ImageUrl = game.CoverUrl });
        }

        if (!string.IsNullOrWhiteSpace(game.IntroUrl))
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Intro", ImageUrl = game.IntroUrl });
        }

        var shotIndex = 1;
        foreach (var url in game.ScreenshotUrls)
        {
            MediaThumbs.Add(new MediaThumbItem { Label = $"Screen {shotIndex++}", ImageUrl = url });
        }

        if (MediaThumbs.Count == 0)
        {
            MediaThumbs.Add(new MediaThumbItem { Label = "Hero" });
            MediaThumbs.Add(new MediaThumbItem { Label = "Gameplay" });
            MediaThumbs.Add(new MediaThumbItem { Label = "Świat" });
            MediaThumbs.Add(new MediaThumbItem { Label = "UI" });
        }

        var preferredPlatform = GamePlatform.Windows;
        Builds.Clear();
        foreach (var build in game.Builds)
        {
            Builds.Add(new BuildRowItem
            {
                BuildId = build.Id,
                Platform = build.Platform,
                Architecture = build.Architecture,
                Version = build.CurrentVersion,
                ExeName = build.ExeName,
                PlatformText = FormatPlatform(build.Platform, build.Architecture),
                MetaText = $"v{build.CurrentVersion} · {build.ExeName}" + (build.IsDefault ? " · domyślny" : ""),
                IsPreferred = build.Platform == preferredPlatform,
                PreferredLabel = build.Platform == preferredPlatform ? "Twoja platforma" : string.Empty
            });
        }

        ShowBuilds = Builds.Count > 0;
        SelectedTab = "Details";
    }

    private void UpdateActionState(GameDto game)
    {
        var preferred = GameListItem.PickPreferredBuild(game.Builds);
        CanAdd = !InLibrary && preferred is not null;
        PrimaryActionLabel = InLibrary
            ? "W bibliotece"
            : preferred is null
                ? "Brak builda"
                : "Dodaj do biblioteki";
    }

    private async Task<bool> IsInLibraryAsync(int gameId)
    {
        if (_scopes is null)
        {
            return false;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            return await library.GetByGameIdAsync(gameId) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatPlatform(GamePlatform platform, string? architecture)
    {
        var name = platform switch
        {
            GamePlatform.Windows => "Windows",
            GamePlatform.Linux => "Linux",
            GamePlatform.MacOS => "macOS",
            _ => platform.ToString()
        };

        return string.IsNullOrWhiteSpace(architecture) ? name : $"{name} ({architecture})";
    }
}

public sealed class MediaThumbItem
{
    public required string Label { get; init; }
    public string? ImageUrl { get; init; }
}

public sealed class BuildRowItem
{
    public required int BuildId { get; init; }
    public required GamePlatform Platform { get; init; }
    public string? Architecture { get; init; }
    public required string Version { get; init; }
    public required string ExeName { get; init; }
    public required string PlatformText { get; init; }
    public required string MetaText { get; init; }
    public bool IsPreferred { get; init; }
    public string PreferredLabel { get; init; } = string.Empty;
}
