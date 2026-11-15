using SGSClient.Core.Localization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;

namespace SGSClient.UI.ViewModels;

public partial class LoginGateViewModel : ObservableObject
{
    private readonly IAuthSession? _auth;
    private readonly IUserApiClient? _users;
    private Func<nint>? _parentWindowHandle;
    private Func<Task>? _onAuthenticated;
    private CancellationTokenSource? _interactiveCts;
    private bool _cancelledByUser;

    private static readonly TimeSpan InteractiveTimeout = TimeSpan.FromMinutes(5);

    public LoginGateViewModel()
        : this(null, null)
    {
    }

    public LoginGateViewModel(
        IAuthSession? auth,
        IUserApiClient? users,
        IClientUserSettingsStore? settings = null,
        ILibraryFolderService? folders = null)
    {
        _auth = auth;
        _users = users;
        _settings = settings;
        _folders = folders;
        IsAuthConfigured = auth?.IsAvailable == true;
        RequiresLogin = IsAuthConfigured;
        StatusText = string.Empty;
        IsFirstRun = IsSetupPending;
        GamesFolder = ClientInstallSettings.GetDefaultGamesRoot();
    }

    private readonly IClientUserSettingsStore? _settings;
    private readonly ILibraryFolderService? _folders;
    private Func<Task>? _onSetupCompleted;

    public bool IsAuthConfigured { get; }

    // First-run wizard: step 1 = sign in (this gate), step 2 = pick the games folder.
    public bool IsSetupPending => _settings is not null && _folders is not null && !_settings.Load().SetupCompleted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeadingText), nameof(SubheadingText))]
    private bool _isFirstRun;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoginStep))]
    private bool _isFolderStep;

    [ObservableProperty]
    private string _gamesFolder = string.Empty;

    public bool IsLoginStep => !IsFolderStep;

    public string HeadingText => IsFirstRun ? Loc.T("Witamy w SGSClient") : Loc.T("Zaloguj się");

    public string SubheadingText => IsFirstRun ? Loc.T("Krok 1 z 2: zaloguj się kontem SGS.") : Loc.T("Użyj konta SGS.");

    public string FolderStepTitle => IsAuthConfigured
        ? Loc.T("Krok 2 z 2: wybierz folder na gry")
        : Loc.T("Wybierz folder na gry");

    public string FolderStepHint =>
        Loc.T("Wybierz folder poza katalogiem aplikacji, aby gry zostały na dysku także po odinstalowaniu SGSClient. Możesz to zmienić później w Ustawieniach.");

    public void SetOnSetupCompleted(Func<Task> onSetupCompleted) => _onSetupCompleted = onSetupCompleted;

    /// <summary>Switches the gate to the folder step when the wizard has not been finished yet.</summary>
    public bool BeginFolderStepIfPending()
    {
        if (!IsSetupPending)
        {
            return false;
        }

        IsFirstRun = true;
        IsFolderStep = true;
        return true;
    }

    // The app registers the default folder on first use, before the user has picked one; drop it again if it is still untouched.
    private async Task RemoveEmptyDefaultFolderAsync(string chosen)
    {
        try
        {
            var defaultRoot = Path.GetFullPath(ClientInstallSettings.GetDefaultGamesRoot());
            if (string.Equals(defaultRoot, Path.GetFullPath(chosen), StringComparison.OrdinalIgnoreCase)
                || !Directory.Exists(defaultRoot)
                || Directory.EnumerateFiles(defaultRoot, "*", SearchOption.AllDirectories)
                    .Any(f => !string.Equals(Path.GetFileName(f), SgsLibraryFolderMarker.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            await _folders!.RemoveFolderAsync(defaultRoot);
            Directory.Delete(defaultRoot, recursive: true);
        }
        catch
        {
            // best effort: a leftover empty folder is harmless
        }
    }

    [RelayCommand]
    private async Task ContinueSetupAsync()
    {
        if (_folders is null || _settings is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(GamesFolder))
        {
            StatusText = Loc.T("Wybierz folder na gry.");
            return;
        }

        IsBusy = true;
        try
        {
            await _folders.SetPrimaryFolderAsync(GamesFolder.Trim());
            await RemoveEmptyDefaultFolderAsync(GamesFolder.Trim());
            var settings = _settings.Load();
            settings.SetupCompleted = true;
            await _settings.SaveAsync(settings);
        }
        catch (Exception ex)
        {
            // Bad path / no permission: stay on the step so the user can pick another folder.
            StatusText = ex.Message;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        StatusText = string.Empty;
        IsFolderStep = false;
        IsFirstRun = false;
        if (_onSetupCompleted is not null)
        {
            await _onSetupCompleted();
        }
    }

    public string VersionText { get; } = BuildVersionText();

    private static string BuildVersionText()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? asm.GetName().Name
            ?? "SGSClient";
        var version = asm.GetName().Version?.ToString() ?? "4.0.0.1";
        return $"{name} {version}";
    }

    public void SetParentWindowHandle(Func<nint> getHandle) => _parentWindowHandle = getHandle;

    public void SetOnAuthenticated(Func<Task> onAuthenticated) => _onAuthenticated = onAuthenticated;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _requiresLogin;

    [RelayCommand]
    private Task LoginAsync() => RunInteractiveAsync(signUp: false);

    [RelayCommand]
    private Task SignUpAsync() => RunInteractiveAsync(signUp: true);

    [RelayCommand]
    private void Cancel()
    {
        _cancelledByUser = true;
        _interactiveCts?.Cancel();
    }

    private string CancelledStatus => _cancelledByUser
        ? string.Empty
        : Loc.T("Upłynął czas na zalogowanie. Spróbuj ponownie.");

    private async Task RunInteractiveAsync(bool signUp)
    {
        if (_auth is null || !_auth.IsAvailable)
        {
            StatusText = _auth?.LastError
                ?? Loc.T("Logowanie nie jest skonfigurowane.");
            return;
        }

        using var cts = new CancellationTokenSource(InteractiveTimeout);
        _interactiveCts = cts;
        _cancelledByUser = false;
        IsBusy = true;
        StatusText = Loc.T("Kontynuuj w przeglądarce…");
        try
        {
            var hwnd = _parentWindowHandle?.Invoke() ?? 0;
            var ok = signUp
                ? await _auth.SignUpAsync(hwnd, cts.Token)
                : await _auth.LoginAsync(hwnd, cts.Token);
            if (cts.IsCancellationRequested)
            {
                StatusText = CancelledStatus;
                return;
            }

            if (!ok || _auth.CurrentUser is null)
            {
                StatusText = _auth.LastError ?? Loc.T("Nie udało się zalogować. Spróbuj ponownie.");
                return;
            }

            if (_users is not null)
            {
                var profile = await _users.GetMeAsync();
                if (profile is not null)
                {
                    _auth.SetCurrentUser(profile);
                }
            }

            StatusText = string.Empty;
            if (_onAuthenticated is not null)
            {
                await _onAuthenticated();
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = CancelledStatus;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            _interactiveCts = null;
            IsBusy = false;
        }
    }

    public async Task<bool> TrySilentEnterAsync()
    {
        if (_auth is null || !_auth.IsAvailable)
        {
            RequiresLogin = false;
            return true;
        }

        try
        {
            if (_auth.IsLoggedIn && _auth.CurrentUser is not null)
            {
                RequiresLogin = false;
                return true;
            }

            var ok = await _auth.TrySilentLoginAsync();
            if (!ok || _auth.CurrentUser is null)
            {
                RequiresLogin = true;
                return false;
            }

            if (_users is not null)
            {
                var profile = await _users.GetMeAsync();
                if (profile is not null)
                {
                    _auth.SetCurrentUser(profile);
                }
            }

            RequiresLogin = false;
            return true;
        }
        catch
        {
            RequiresLogin = true;
            return false;
        }
    }
}
