using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.Achievements;
using SGSClient.Core.DTOs;
using SGSClient.Core.Install;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>
/// One achievement of the Steamworks-style table. Table rows are read-only; editing happens on a
/// <see cref="Clone"/> shown in a dialog (see <see cref="AchievementsEditorViewModel.BeginEdit"/>).
/// Numbers are edited as text.
/// </summary>
public partial class StudioAchievementItem : ObservableObject
{
    public const int KindOneShot = 0;
    public const int KindCounter = 1;
    public const int KindStat = 2;

    /// <summary>Language the panel edits next to the base text; other codes are carried through untouched.</summary>
    private const string EditedLanguage = "en";

    private IReadOnlyDictionary<string, AchievementTextDto> _otherTranslations =
        new Dictionary<string, AchievementTextDto>();

    /// <summary>True while the key follows the title (a new achievement whose key nobody typed).</summary>
    private bool _keyFollowsTitle;

    private bool _settingKey;

    /// <summary>True until the first save: the key is the game's public id, so it is locked afterwards.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKeyEditable))]
    [NotifyPropertyChangedFor(nameof(IsSaved))]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    public partial bool IsNew { get; set; }

    public bool IsKeyEditable => IsNew;

    public bool IsSaved => !IsNew;

    /// <summary>Position in the table ("1", "2"…), set by the editor.</summary>
    [ObservableProperty] public partial string RowId { get; set; } = string.Empty;

    /// <summary>Resolves an uploaded (relative) icon for the preview.</summary>
    public string ApiBaseUrl { get; init; } = string.Empty;

    /// <summary>Stat API names the dialog offers for a stat-driven achievement.</summary>
    [ObservableProperty] public partial IReadOnlyList<string> AvailableStats { get; set; } = [];

    /// <summary>First problem with the dialog's fields; empty when it can be saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string ValidationError { get; set; } = string.Empty;

    public bool HasValidationError => ValidationError.Length > 0;

    public bool CanSave => !HasValidationError;

    [ObservableProperty] public partial string EditKey { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(PreviewLockedTitle))]
    public partial string EditTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewLockedDescription))]
    public partial string EditDescription { get; set; } = string.Empty;

    [ObservableProperty] public partial string EditTitleEn { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDescriptionEn { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IconPreviewUrl))]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    [NotifyPropertyChangedFor(nameof(LockedColumnUrl))]
    [NotifyPropertyChangedFor(nameof(HasLockedColumnIcon))]
    [NotifyPropertyChangedFor(nameof(PreviewLockedIconUrl))]
    [NotifyPropertyChangedFor(nameof(HasPreviewLockedIcon))]
    public partial string EditIconUrl { get; set; } = string.Empty;

    /// <summary>Unachieved icon; empty = players see the achieved icon dimmed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LockedIconPreviewUrl))]
    [NotifyPropertyChangedFor(nameof(HasLockedIcon))]
    [NotifyPropertyChangedFor(nameof(LockedColumnUrl))]
    [NotifyPropertyChangedFor(nameof(HasLockedColumnIcon))]
    [NotifyPropertyChangedFor(nameof(LockedColumnOpacity))]
    [NotifyPropertyChangedFor(nameof(PreviewLockedIconUrl))]
    [NotifyPropertyChangedFor(nameof(HasPreviewLockedIcon))]
    public partial string EditLockedIconUrl { get; set; } = string.Empty;

    [ObservableProperty] public partial string EditPoints { get; set; } = "10";

    /// <summary><see cref="KindOneShot"/>, <see cref="KindCounter"/> or <see cref="KindStat"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsTarget))]
    [NotifyPropertyChangedFor(nameof(IsStatKind))]
    [NotifyPropertyChangedFor(nameof(IsOneShotKind))]
    [NotifyPropertyChangedFor(nameof(IsCounterKind))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewProgress))]
    [NotifyPropertyChangedFor(nameof(PreviewProgressText))]
    public partial int EditKindIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(PreviewProgressText))]
    [NotifyPropertyChangedFor(nameof(PreviewProgressMax))]
    public partial string EditTarget { get; set; } = string.Empty;

    /// <summary>API name of the stat that drives the progress (stat kind only).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial string EditStatKey { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HiddenText))]
    [NotifyPropertyChangedFor(nameof(PreviewLockedTitle))]
    [NotifyPropertyChangedFor(nameof(PreviewLockedDescription))]
    [NotifyPropertyChangedFor(nameof(PreviewLockedIconUrl))]
    [NotifyPropertyChangedFor(nameof(HasPreviewLockedIcon))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewProgress))]
    public partial bool EditHidden { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    public partial int UnlockedCount { get; set; }

    public string StatsText => IsNew ? string.Empty : Loc.F("Odblokowało: {0}", UnlockedCount);

    public bool ShowsTarget => EditKindIndex != KindOneShot;
    public bool IsStatKind => EditKindIndex == KindStat;

    // Two-way friendly flags for radio buttons.
    public bool IsOneShotKind { get => EditKindIndex == KindOneShot; set { if (value) EditKindIndex = KindOneShot; } }
    public bool IsCounterKind { get => EditKindIndex == KindCounter; set { if (value) EditKindIndex = KindCounter; } }
    public bool IsStatKindChecked { get => EditKindIndex == KindStat; set { if (value) EditKindIndex = KindStat; } }

    public string? IconPreviewUrl =>
        InstallPathHelper.TryResolveDownloadUrl(ApiBaseUrl, EditIconUrl.Trim());

    public bool HasIcon => IconPreviewUrl is not null;

    public string? LockedIconPreviewUrl =>
        InstallPathHelper.TryResolveDownloadUrl(ApiBaseUrl, EditLockedIconUrl.Trim());

    public bool HasLockedIcon => LockedIconPreviewUrl is not null;

    /// <summary>What the "Unachieved icon" column shows: the locked icon, or the achieved one dimmed.</summary>
    public string? LockedColumnUrl => LockedIconPreviewUrl ?? IconPreviewUrl;

    public bool HasLockedColumnIcon => LockedColumnUrl is not null;

    public double LockedColumnOpacity => HasLockedIcon ? 1 : 0.35;

    public string DisplayTitle => string.IsNullOrWhiteSpace(EditTitle) ? Loc.T("(bez nazwy)") : EditTitle;

    public string HiddenText => EditHidden ? Loc.T("Tak") : string.Empty;

    /// <summary>"Jednorazowe", "Licznik: 10" or "num_games ≥ 10".</summary>
    public string ProgressText =>
        EditKindIndex == KindOneShot || string.IsNullOrWhiteSpace(EditTarget) ? Loc.T("Jednorazowe")
        : EditKindIndex == KindCounter || string.IsNullOrWhiteSpace(EditStatKey) ? Loc.F("Licznik: {0}", EditTarget.Trim())
        : $"{EditStatKey.Trim()} ≥ {EditTarget.Trim()}";

    // ---- "how players see it" preview ----------------------------------------------------

    public string PreviewLockedTitle => EditHidden ? Loc.T("Ukryte osiągnięcie") : DisplayTitle;

    public string PreviewLockedDescription => EditHidden ? Loc.T("Odblokuj, aby poznać szczegóły.") : EditDescription;

    public string? PreviewLockedIconUrl => EditHidden ? null : LockedColumnUrl;

    public bool HasPreviewLockedIcon => PreviewLockedIconUrl is not null;

    public bool ShowPreviewProgress => ShowsTarget && !EditHidden;

    public string PreviewProgressText => $"0 / {EditTarget.Trim()}";

    public double PreviewProgressMax =>
        double.TryParse(EditTarget, NumberStyles.None, CultureInfo.InvariantCulture, out var max) && max > 0 ? max : 1;

    /// <summary>A blank achievement for the "New achievement" dialog; its key follows the title until typed.</summary>
    public static StudioAchievementItem CreateNew(string apiBaseUrl) =>
        new() { IsNew = true, ApiBaseUrl = apiBaseUrl, _keyFollowsTitle = true };

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
            EditLockedIconUrl = dto.LockedIconUrl ?? string.Empty,
            EditStatKey = dto.StatKey ?? string.Empty,
            EditPoints = dto.Points.ToString(CultureInfo.InvariantCulture),
            EditTarget = dto.Target?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            EditKindIndex = dto.StatKey is not null ? KindStat : dto.Target is not null ? KindCounter : KindOneShot,
            EditHidden = dto.Hidden,
            UnlockedCount = dto.UnlockedCount,
            _otherTranslations = translations
                .Where(kv => !string.Equals(kv.Key, EditedLanguage, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value)
        };
    }

    /// <summary>An independent copy for the edit dialog, so Cancel leaves the table untouched.</summary>
    public StudioAchievementItem Clone() => new()
    {
        IsNew = IsNew,
        ApiBaseUrl = ApiBaseUrl,
        RowId = RowId,
        EditKey = EditKey,
        EditTitle = EditTitle,
        EditDescription = EditDescription,
        EditTitleEn = EditTitleEn,
        EditDescriptionEn = EditDescriptionEn,
        EditIconUrl = EditIconUrl,
        EditLockedIconUrl = EditLockedIconUrl,
        EditPoints = EditPoints,
        EditKindIndex = EditKindIndex,
        EditTarget = EditTarget,
        EditStatKey = EditStatKey,
        EditHidden = EditHidden,
        UnlockedCount = UnlockedCount,
        _otherTranslations = _otherTranslations,
        _keyFollowsTitle = _keyFollowsTitle
    };

    partial void OnEditTitleChanged(string value)
    {
        if (IsNew && _keyFollowsTitle)
        {
            _settingKey = true;
            EditKey = AchievementRules.SuggestKey(value);
            _settingKey = false;
        }
    }

    partial void OnEditKeyChanged(string value)
    {
        if (!_settingKey)
        {
            _keyFollowsTitle = false; // the creator typed a key: stop overwriting it
        }
    }

    partial void OnEditKindIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsStatKindChecked));
        if (value == KindStat && string.IsNullOrWhiteSpace(EditStatKey) && AvailableStats.Count > 0)
        {
            EditStatKey = AvailableStats[0];
        }
    }

    /// <summary>
    /// Parses the number fields and applies the kind (a one-shot drops the target, a counter the stat);
    /// range and key rules are checked by <c>AchievementRules</c>.
    /// </summary>
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
        if (ShowsTarget && !string.IsNullOrWhiteSpace(EditTarget))
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
            LockedIconUrl = string.IsNullOrWhiteSpace(EditLockedIconUrl) ? null : EditLockedIconUrl.Trim(),
            StatKey = IsStatKind && !string.IsNullOrWhiteSpace(EditStatKey) ? EditStatKey.Trim() : null,
            Translations = translations.Count == 0 ? null : translations,
            Points = points,
            Hidden = EditHidden,
            Target = target,
            SortOrder = sortOrder
        };
        return true;
    }
}
