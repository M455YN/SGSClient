using SGSClient.Core.Localization;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.DTOs;

namespace SGSClient.UI.ViewModels;

public sealed class StudioScreenshotItem
{
    public required int Id { get; init; }
    public required string ImageUrl { get; init; }
}

public sealed class StudioBuildItem
{
    public required string PlatformText { get; init; }
    public required string Version { get; init; }
    public required string ExeName { get; init; }
    public bool IsDefault { get; init; }
    public string MetaText => IsDefault ? Loc.F("{0} · domyślny", ExeName) : ExeName;
}

public partial class StudioVersionItem : ObservableObject
{
    public required int Id { get; init; }
    public required string Version { get; init; }
    public string? PlatformLabel { get; init; }
    public DateTimeOffset PublishedAtUtc { get; init; }
    public bool IsCurrent { get; init; }

    [ObservableProperty] public partial string EditNotes { get; set; } = string.Empty;

    public string MetaText =>
        (string.IsNullOrWhiteSpace(PlatformLabel) ? "build" : PlatformLabel)
        + " · "
        + PublishedAtUtc.LocalDateTime.ToString("d MMM yyyy HH:mm", Loc.Culture)
        + (IsCurrent ? Loc.T(" · aktualna") : "");

    public static StudioVersionItem FromDto(GameBuildVersionDto dto)
    {
        var item = new StudioVersionItem
        {
            Id = dto.Id,
            Version = dto.Version,
            PlatformLabel = dto.PlatformLabel,
            PublishedAtUtc = dto.PublishedAtUtc,
            IsCurrent = dto.IsCurrent
        };
        item.ApplyDto(dto);
        return item;
    }

    public void ApplyDto(GameBuildVersionDto dto) => EditNotes = dto.ReleaseNotes ?? string.Empty;
}

public sealed class StudioStatBar
{
    public required string Label { get; init; }
    public required int Value { get; init; }
    public required int Max { get; init; }
    public double BarHeight => 8 + (Max <= 0 ? 0 : 48.0 * Value / Max);
    public string Tooltip => $"{Label}: {Value}";
}
