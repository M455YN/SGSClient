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
using SGSClient.Core.Localization;
using SGSClient.Core.Models;
using SGSClient.Core.Playtime;

namespace SGSClient.UI.ViewModels;

public sealed class MediaThumbItem
{
    public required string Label { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsImage { get; init; }
    public bool IsVideo { get; init; }
    public bool HasMedia => !string.IsNullOrWhiteSpace(ImageUrl);
}

public partial class BuildRowItem : ObservableObject
{
    public required int BuildId { get; init; }
    public required GamePlatform Platform { get; init; }
    public string? Architecture { get; init; }
    public required string Version { get; init; }
    public required string ExeName { get; init; }
    public required string PlatformText { get; init; }
    public string MetaText { get; set; } = string.Empty;
    public bool IsPreferred { get; init; }
    public string PreferredLabel { get; init; } = string.Empty;

    public ObservableCollection<VersionOptionItem> Versions { get; } = [];

    [ObservableProperty]
    public partial VersionOptionItem? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial bool HasVersionPicker { get; set; }
}

public sealed class VersionOptionItem
{
    public required string Version { get; init; }
    public required string DisplayText { get; init; }
    public bool IsCurrent { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }

    public static VersionOptionItem FromCurrent(string version) => new()
    {
        Version = version,
        DisplayText = Loc.F("v{0} (najnowsza)", version),
        IsCurrent = true,
        PublishedAtUtc = DateTimeOffset.UtcNow
    };

    public static VersionOptionItem FromDto(GameBuildVersionDto dto) => new()
    {
        Version = dto.Version,
        DisplayText = dto.IsCurrent
            ? Loc.F("v{0} (najnowsza)", dto.Version)
            : $"v{dto.Version} · {dto.PublishedAtUtc.LocalDateTime:g}",
        IsCurrent = dto.IsCurrent,
        PublishedAtUtc = dto.PublishedAtUtc
    };
}
