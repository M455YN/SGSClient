using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

public static class MaintenanceNotice
{
    public static string Text(IAuthSession? auth) =>
        auth?.CurrentUser?.IsSuperAdmin == true
            ? Loc.T("Trwa przerwa techniczna — masz pełny dostęp jako superadmin")
            : Loc.T("Trwa przerwa techniczna — możesz pobierać gry, pozostałe funkcje są chwilowo niedostępne");
}
