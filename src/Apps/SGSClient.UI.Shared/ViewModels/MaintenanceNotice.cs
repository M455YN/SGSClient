using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

public static class MaintenanceNotice
{
    // Superadmin keeps full access during maintenance, so no banner for them.
    public static bool IsVisible(bool isMaintenance, IAuthSession? auth) =>
        isMaintenance && auth?.CurrentUser?.IsSuperAdmin != true;

    public static string Text() =>
        Loc.T("Trwa przerwa techniczna — możesz pobierać gry, pozostałe funkcje są chwilowo niedostępne");
}

/// <summary>Bar shown while the client talks to a test API (the API's DevelopmentBanner setting).</summary>
public static class DevelopmentNotice
{
    public static string Title() => Loc.T("Wersja deweloperska");

    public static string Text() =>
        Loc.T("Korzystasz z testowego serwera SGS. Dane mogą zostać zmienione lub usunięte.");
}
