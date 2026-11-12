using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>
/// One row of the stats table (Steamworks "Stats"). Rows are read-only; editing happens on a <see cref="Clone"/>
/// shown in a dialog. Numbers are edited as text; empty = no limit.
/// </summary>
public partial class StudioStatItem : ObservableObject
{
    public static IReadOnlyList<string> TypeNames { get; } = ["INT", "FLOAT"];

    /// <summary>True until the first save: the key is the game's public id, so it is locked afterwards.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKeyEditable))]
    public partial bool IsNew { get; set; }

    public bool IsKeyEditable => IsNew;

    [ObservableProperty] public partial string RowId { get; set; } = string.Empty;

    /// <summary>First problem with the dialog's fields; empty when it can be saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string ValidationError { get; set; } = string.Empty;

    public bool HasValidationError => ValidationError.Length > 0;

    public bool CanSave => !HasValidationError;
    [ObservableProperty] public partial string EditKey { get; set; } = string.Empty;
    [ObservableProperty] public partial string EditDisplayName { get; set; } = string.Empty;

    /// <summary>Index into <see cref="TypeNames"/> (0 = INT, 1 = FLOAT), for a ComboBox.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TypeText))]
    [NotifyPropertyChangedFor(nameof(IsIntType))]
    [NotifyPropertyChangedFor(nameof(IsFloatType))]
    public partial int EditTypeIndex { get; set; }

    // Two-way friendly flags for radio buttons.
    public bool IsIntType { get => EditTypeIndex == 0; set { if (value) EditTypeIndex = 0; } }
    public bool IsFloatType { get => EditTypeIndex == 1; set { if (value) EditTypeIndex = 1; } }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    public partial string EditMin { get; set; } = "0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RangeText))]
    public partial string EditMax { get; set; } = string.Empty;

    [ObservableProperty] public partial string EditDefault { get; set; } = "0";
    [ObservableProperty] public partial string EditMaxChange { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IncrementOnlyText))]
    public partial bool EditIncrementOnly { get; set; }

    public string TypeText => TypeNames[Math.Clamp(EditTypeIndex, 0, TypeNames.Count - 1)];

    public string RangeText =>
        $"{(string.IsNullOrWhiteSpace(EditMin) ? "−∞" : EditMin.Trim())} … {(string.IsNullOrWhiteSpace(EditMax) ? "∞" : EditMax.Trim())}";

    public string IncrementOnlyText => EditIncrementOnly ? Loc.T("Tak") : string.Empty;

    public static StudioStatItem FromDto(GameStatDefinitionDto dto) => new()
    {
        EditKey = dto.Key,
        EditDisplayName = dto.DisplayName,
        EditTypeIndex = (int)dto.Type,
        EditMin = Format(dto.MinValue),
        EditMax = Format(dto.MaxValue),
        EditDefault = Format(dto.DefaultValue),
        EditMaxChange = Format(dto.MaxChange),
        EditIncrementOnly = dto.IncrementOnly
    };

    /// <summary>An independent copy for the edit dialog, so Cancel leaves the table untouched.</summary>
    public StudioStatItem Clone() => new()
    {
        IsNew = IsNew,
        RowId = RowId,
        EditKey = EditKey,
        EditDisplayName = EditDisplayName,
        EditTypeIndex = EditTypeIndex,
        EditMin = EditMin,
        EditMax = EditMax,
        EditDefault = EditDefault,
        EditMaxChange = EditMaxChange,
        EditIncrementOnly = EditIncrementOnly
    };

    /// <summary>Parses the number fields (dot or comma); range rules are checked by <c>AchievementRules.ValidateStats</c>.</summary>
    public bool TryToDto(int sortOrder, out GameStatDefinitionDto? dto, out string? error)
    {
        dto = null;
        error = null;
        if (!TryParse(EditMin, out var min) || !TryParse(EditMax, out var max)
            || !TryParse(EditDefault, out var defaultValue) || !TryParse(EditMaxChange, out var maxChange))
        {
            error = Loc.F("Statystyka „{0}”: wartości muszą być liczbami.", EditKey.Trim());
            return false;
        }

        dto = new GameStatDefinitionDto
        {
            Key = EditKey.Trim(),
            DisplayName = EditDisplayName.Trim(),
            Type = EditTypeIndex == 1 ? GameStatType.Float : GameStatType.Int,
            MinValue = min,
            MaxValue = max,
            DefaultValue = defaultValue ?? 0,
            MaxChange = maxChange,
            IncrementOnly = EditIncrementOnly,
            SortOrder = sortOrder
        };
        return true;
    }

    private static string Format(double? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool TryParse(string text, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
