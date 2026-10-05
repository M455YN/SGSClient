using CommunityToolkit.Mvvm.ComponentModel;
using SGSClient.Core.DTOs;
using SGSClient.Core.Enums;

namespace SGSClient.UI.ViewModels;

public partial class AdminUserItem : ObservableObject
{
    public required int Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? PlayerTag { get; init; }
    public bool IsRoleLocked { get; init; }
    public int OwnedGamesCount { get; init; }
    public string CreatedText { get; init; } = "—";
    public string LastLoginText { get; init; } = "—";

    public event Action<AdminUserItem, UserRole>? RoleChangeRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleLabel))]
    [NotifyPropertyChangedFor(nameof(RoleIndex))]
    public partial UserRole Role { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RoleOptions))]
    [NotifyPropertyChangedFor(nameof(RoleHint))]
    public partial bool CanEditRole { get; set; }

    [ObservableProperty] public partial bool IsSelf { get; set; }
    [ObservableProperty] public partial bool ViewerIsSuperAdmin { get; set; }

    public int RoleIndex
    {
        get => (int)Role;
        set
        {
            if (value >= 0 && (UserRole)value != Role)
            {
                RoleChangeRequested?.Invoke(this, (UserRole)value);
            }
        }
    }

    public IReadOnlyList<string> RoleOptions =>
        CanEditRole && !ViewerIsSuperAdmin ? AdminRoles.AssignableByAdmin : AdminRoles.All;

    public string RoleLabel => AdminRoles.Label(Role);

    public string RoleHint => IsRoleLocked
        ? "Konto właściciela — rola na stałe"
        : IsSelf
            ? "Nie możesz zmienić własnej roli"
            : CanEditRole
                ? string.Empty
                : "Tylko superadmin zmienia role adminów";

    public string Initials => string.IsNullOrWhiteSpace(DisplayName)
        ? "?"
        : DisplayName.Trim()[..1].ToUpperInvariant();

    public string GamesCountText => FormatGamesCount(OwnedGamesCount);

    public string MetaText =>
        (string.IsNullOrWhiteSpace(Email) ? $"#{Id}" : Email!)
        + (string.IsNullOrWhiteSpace(PlayerTag) ? "" : $" · @{PlayerTag}")
        + $" · {GamesCountText}"
        + $" · dołączono {CreatedText} · ostatnio {LastLoginText}";

    /// <summary>Combo label for owner transfer: "Name (email)".</summary>
    public string OwnerPickerLabel => string.IsNullOrWhiteSpace(Email)
        ? $"{DisplayName} (#{Id})"
        : $"{DisplayName} ({Email})";

    public void ApplyDto(AdminUserDto user) => Role = user.Role;

    public void OnRoleReverted() => OnPropertyChanged(nameof(RoleIndex));

    public static AdminUserItem FromDto(AdminUserDto user) => new()
    {
        Id = user.Id,
        DisplayName = user.DisplayName,
        Email = user.Email,
        PlayerTag = user.PlayerTag,
        IsRoleLocked = user.IsRoleLocked,
        OwnedGamesCount = user.OwnedGamesCount,
        Role = user.Role,
        CreatedText = user.CreatedAtUtc.LocalDateTime.ToString("d MMM yyyy"),
        LastLoginText = user.LastLoginUtc?.LocalDateTime.ToString("d MMM yyyy") ?? "—"
    };

    private static string FormatGamesCount(int count) => count switch
    {
        0 => "0 gier",
        1 => "1 gra",
        var n when n is >= 2 and <= 4 => $"{n} gry",
        _ => $"{count} gier"
    };
}

public static class AdminRoles
{
    public static IReadOnlyList<string> All { get; } = ["Użytkownik", "Twórca", "Admin", "Superadmin"];

    public static IReadOnlyList<string> AssignableByAdmin { get; } = [All[0], All[1]];

    public static string Label(UserRole role) => All[Math.Clamp((int)role, 0, All.Count - 1)];
}
