using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.DTOs;
using SGSClient.Core.Install;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>One editable row of an achievements editor. Numbers are edited as text.</summary>
public partial class StudioAchievementItem : ObservableObject
{
    /// <summary>Language the panel edits next to the base text; other codes are carried through untouched.</summary>
    private const string EditedLanguage = "en";

    private IReadOnlyDictionary<string, AchievementTextDto> _otherTranslations =
        new Dictionary<string, AchievementTextDto>();

    /// <summary>True until the first save: the key is the game's public id, so it is locked afterwards.</summary>
    public bool IsNew { get; init; }

    public bool IsKeyEditable => IsNew;

    /// <summary>Resolves an uploaded (relative) icon for the preview.</summary>
    public string ApiBaseUrl { get; init; } = string.Empty;

    [ObservableProperty] public partial string EditKey { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDescription { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditTitleEn { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDescriptionEn { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconPreviewUrl))]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    public partial string EditIconUrl { get; set; } = string.Empty;

    [ObservableProperty] public partial string EditPoints { get; set; } = "10";
    [ObservableProperty] public partial string EditTarget { get; set; } = string.Empty;
    [ObservableProperty] public partial bool EditHidden { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    public partial int UnlockedCount { get; set; }

    public string StatsText => IsNew ? string.Empty : Loc.F("Odblokowało: {0}", UnlockedCount);

    public string? IconPreviewUrl =>
        InstallPathHelper.TryResolveDownloadUrl(ApiBaseUrl, EditIconUrl.Trim());

    public bool HasIcon => IconPreviewUrl is not null;

    public static StudioAchievementItem FromDto(AchievementDefinitionDto dto, string apiBaseUrl)
    {
        var translations = dto.Translations ?? new Dictionary<string, AchievementTextDto>();
        translations.TryGetValue(EditedLanguage, out var english);
        return new StudioAchievementItem
        {
            ApiBaseUrl = apiBaseUrl,
            EditKey = dto.Key,
            EditTitle = dto.Title,
            EditDescription = dto.Description,
            EditTitleEn = english?.Title ?? string.Empty,
            EditDescriptionEn = english?.Description ?? string.Empty,
            EditIconUrl = dto.IconUrl ?? string.Empty,
            EditPoints = dto.Points.ToString(CultureInfo.InvariantCulture),
            EditTarget = dto.Target?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            EditHidden = dto.Hidden,
            UnlockedCount = dto.UnlockedCount,
            _otherTranslations = translations
                .Where(kv => !string.Equals(kv.Key, EditedLanguage, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }

    /// <summary>Parses the number fields; range and key rules are checked by <c>AchievementRules</c>.</summary>
    public bool TryToDto(int sortOrder, out AchievementDefinitionDto? dto, out string? error)
    {
        dto = null;
        error = null;

        var points = 0;
        if (!string.IsNullOrWhiteSpace(EditPoints)
            && !int.TryParse(EditPoints.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out points))
        {
            error = Loc.F("Osiągnięcie „{0}”: punkty muszą być liczbą całkowitą.", EditKey.Trim());
            return false;
        }

        int? target = null;
        if (!string.IsNullOrWhiteSpace(EditTarget))
        {
            if (!int.TryParse(EditTarget.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                error = Loc.F("Osiągnięcie „{0}”: cel licznika musi być liczbą całkowitą.", EditKey.Trim());
                return false;
            }

            target = parsed;
        }

        var translations = new Dictionary<string, AchievementTextDto>(_otherTranslations);
        if (!string.IsNullOrWhiteSpace(EditTitleEn) || !string.IsNullOrWhiteSpace(EditDescriptionEn))
        {
            translations[EditedLanguage] = new AchievementTextDto
            {
                Title = EditTitleEn.Trim(),
                Description = EditDescriptionEn.Trim()
            };
        }

        dto = new AchievementDefinitionDto
        {
            Key = EditKey.Trim(),
            Title = EditTitle.Trim(),
            Description = EditDescription.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(EditIconUrl) ? null : EditIconUrl.Trim(),
            Translations = translations.Count == 0 ? null : translations,
            Points = points,
            Hidden = EditHidden,
            Target = target,
            SortOrder = sortOrder
        };
        return true;
    }
}
