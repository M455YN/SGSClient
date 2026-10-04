using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SGSClient.Core.Interfaces;

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

    public LoginGateViewModel(IAuthSession? auth, IUserApiClient? users)
    {
        _auth = auth;
        _users = users;
        IsAuthConfigured = auth?.IsAvailable == true;
        RequiresLogin = IsAuthConfigured;
        StatusText = string.Empty;
    }

    public bool IsAuthConfigured { get; }

    public string VersionText { get; } = BuildVersionText();

    private static string BuildVersionText()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var name = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product
            ?? asm.GetName().Name
            ?? "SGSClient";
        var version = asm.GetName().Version?.ToString() ?? "4.0.0.0";
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
        : "Upłynął czas na zalogowanie. Spróbuj ponownie.";

    private async Task RunInteractiveAsync(bool signUp)
    {
        if (_auth is null || !_auth.IsAvailable)
        {
            StatusText = _auth?.LastError
                ?? "Azure AD B2C jest wyłączone. Ustaw AzureAd w appsettings.";
            return;
        }

        using var cts = new CancellationTokenSource(InteractiveTimeout);
        _interactiveCts = cts;
        _cancelledByUser = false;
        IsBusy = true;
        StatusText = "Kontynuuj w przeglądarce…";
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
                StatusText = _auth.LastError ?? "Nie udało się zalogować. Spróbuj ponownie.";
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
