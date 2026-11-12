using System.Globalization;
using SGSClient.Core.DTOs;
using SGSClient.Core.Install;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>One achievement as a player sees it on the game page.</summary>
public sealed class PlayerAchievementItem
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    /// <summary>Achieved icon when unlocked; the unachieved icon (or the achieved one, dimmed) when locked.</summary>
    public string? IconUrl { get; init; }
    public bool HasIcon => IconUrl is not null;

    /// <summary>Below 1 when a locked achievement has no unachieved icon of its own (stands in for greyscale).</summary>
    public double IconOpacity { get; init; } = 1;
    public bool IsUnlocked { get; init; }
    public DateTimeOffset? UnlockedAtUtc { get; init; }
    public double RarityPercent { get; init; }
    public string RarityText { get; init; } = string.Empty;
    public string UnlockedText { get; init; } = string.Empty;
    public bool ShowProgress { get; init; }
    public double ProgressValue { get; init; }
    public double ProgressMax { get; init; } = 1;
    public required string StateText { get; init; }
    public required string MetaText { get; init; }

    public static PlayerAchievementItem FromDto(GameAchievementDto dto, string apiBaseUrl)
    {
        var unlocked = dto.UnlockedAtUtc is not null;
        var masked = dto.Hidden && !unlocked;
        var progress = dto.Target is int target && !unlocked;
        var lockedIcon = unlocked ? null : InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, dto.LockedIconUrl);

        return new PlayerAchievementItem
        {
            Title = masked ? Loc.T("Ukryte osiągnięcie") : dto.Title,
            Description = masked ? Loc.T("Odblokuj, aby poznać szczegóły.") : dto.Description,
            IconUrl = lockedIcon ?? InstallPathHelper.TryResolveDownloadUrl(apiBaseUrl, dto.IconUrl),
            IconOpacity = unlocked || lockedIcon is not null ? 1 : 0.35,
            IsUnlocked = unlocked,
            UnlockedAtUtc = dto.UnlockedAtUtc,
            RarityPercent = dto.RarityPercent,
            RarityText = Loc.F("{0}% graczy odblokowało to osiągnięcie", dto.RarityPercent.ToString("0.0", CultureInfo.InvariantCulture)),
            UnlockedText = unlocked
                ? Loc.F("Odblokowano: {0}", dto.UnlockedAtUtc!.Value.LocalDateTime.ToString("d MMM yyyy, HH:mm", Loc.Culture))
                : progress && !masked ? $"{dto.Progress} / {dto.Target}" : Loc.T("Zablokowane"),
            ShowProgress = progress && !masked,
            ProgressValue = dto.Progress,
            ProgressMax = dto.Target ?? 1,
            StateText = unlocked
                ? Loc.F("Odblokowano {0}", dto.UnlockedAtUtc!.Value.LocalDateTime.ToString("d", Loc.Culture))
                : progress && !masked
                    ? $"{dto.Progress} / {dto.Target}"
                    : Loc.T("Zablokowane"),
            MetaText = Loc.F(
                "{0} pkt · {1}% graczy",
                dto.Points,
                dto.RarityPercent.ToString("0.#", CultureInfo.InvariantCulture))
        };
    }
}

/// <summary>One game's line on the signed-in player's profile.</summary>
public sealed class AchievementSummaryItem
{
    public int GameId { get; init; }
    public required string Title { get; init; }
    public required string ProgressText { get; init; }
    public required string PointsText { get; init; }
    public double Fraction { get; init; }

    public static AchievementSummaryItem FromDto(UserAchievementSummaryDto dto) => new()
    {
        GameId = dto.GameId,
        Title = string.IsNullOrWhiteSpace(dto.GameTitle) ? $"#{dto.GameId}" : dto.GameTitle,
        ProgressText = $"{dto.Unlocked} / {dto.Total}",
        PointsText = Loc.F("{0} pkt", dto.PointsEarned),
        Fraction = dto.Total <= 0 ? 0 : (double)dto.Unlocked / dto.Total
    };
}
