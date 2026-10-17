using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;

namespace SGSClient.UI.ViewModels;

/// <summary>The sign-in hint shown on every publish screen.</summary>
public static class AuthHints
{
    public static string For(IAuthSession? auth) =>
        auth?.IsAvailable != true
            ? Loc.T("Logowanie jest wyłączone — publikacja nastąpi bez konta.")
            : auth.IsLoggedIn
                ? $"Zalogowano jako {auth.CurrentUser?.DisplayName ?? "gracz"}."
                : Loc.T("Zaloguj się w Profilu przed publikacją.");
}
