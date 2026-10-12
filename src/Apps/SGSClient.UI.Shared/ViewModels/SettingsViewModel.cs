using SGSClient.Core.Localization;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Library;

namespace SGSClient.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly ILibraryFolderService? _libraryFolders;
    private readonly IAuthSession? _auth;
    private readonly IClientUserSettingsStore? _userSettings;
    private readonly IThemeApplier? _themes;
    private readonly string _accountStatusFallback;
    private readonly bool _loaded;

    private static readonly string[] ThemeValues = ["system", "light", "dark"];
    private static readonly string[] LanguageValues = ["system", "pl", "en"];

    /// <summary>Design-time / fallback constructor (no services).</summary>
    public SettingsViewModel()
    {
        Title = Loc.T("Ustawienia");
        StoreStatus = Loc.T("Brak adresu serwera");
        _accountStatusFallback = Loc.T("Logowanie wyłączone");
        AccountStatus = _accountStatusFallback;
        ThemeOptions = [Loc.T("Jak w systemie"), Loc.T("Jasny"), Loc.T("Ciemny")];
        LanguageOptions = [Loc.T("Jak w systemie"), Loc.T("Polski"), "English"];
        LibrarySummary = Loc.T("Foldery, w których klient szuka i instaluje gry.");
        AboutText = ReadVersion();
    }

    public SettingsViewModel(
        IConfiguration configuration,
        IServiceScopeFactory scopes,
        ILibraryFolderService libraryFolders,
        IAuthSession? auth = null,
        IClientUserSettingsStore? userSettings = null,
        IThemeApplier? themes = null)
    {
        _scopes = scopes;
        _libraryFolders = libraryFolders;
        _auth = auth;
        _userSettings = userSettings;
        _themes = themes;
        Title = Loc.T("Ustawienia");

        var api = configuration.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()
                  ?? new ApiClientSettings();
        var azure = configuration.GetSection(AzureAdSettings.SectionName).Get<AzureAdSettings>()
                    ?? new AzureAdSettings();

        StoreStatus = string.IsNullOrWhiteSpace(api.BaseUrl)
            ? Loc.T("Brak adresu serwera")
            : api.BaseUrl.TrimEnd('/');
        _accountStatusFallback = azure.IsConfigured
            ? Loc.T("Możesz zalogować się w zakładce Profil")
            : azure.Enabled
                ? Loc.T("Logowanie wymaga dokończenia konfiguracji")
                : Loc.T("Logowanie jest wyłączone");
        AccountStatus = _accountStatusFallback;
        ThemeOptions = [Loc.T("Jak w systemie"), Loc.T("Jasny"), Loc.T("Ciemny")];
        LanguageOptions = [Loc.T("Jak w systemie"), Loc.T("Polski"), "English"];
        LibrarySummary = Loc.T("Foldery, w których klient szuka i instaluje gry.");
        AboutText = ReadVersion();
        ReloadFolders();
        RefreshAccountStatus();

        var saved = _userSettings?.Load() ?? new ClientUserSettings();
        SelectedThemeIndex = Math.Max(0, Array.IndexOf(ThemeValues, saved.Theme));
        SelectedLanguageIndex = Math.Max(0, Array.IndexOf(LanguageValues, saved.Language));
        _loaded = true;
    }

    public string Title { get; }
    public string StoreStatus { get; }

    [ObservableProperty]
    public partial string AccountStatus { get; set; } = string.Empty;

    public IReadOnlyList<string> ThemeOptions { get; }
    public IReadOnlyList<string> LanguageOptions { get; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial int SelectedLanguageIndex { get; set; }

    [ObservableProperty]
    public partial string LanguageHint { get; set; } = string.Empty;

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (!_loaded || value < 0 || value >= ThemeValues.Length)
        {
            return;
        }

        _themes?.Apply(ThemeValues[value]);
        _ = SavePreferencesAsync();
    }

    partial void OnSelectedLanguageIndexChanged(int value)
    {
        if (!_loaded || value < 0 || value >= LanguageValues.Length)
        {
            return;
        }

        LanguageHint = Loc.T("Zmiana języka zacznie działać po ponownym uruchomieniu aplikacji.");
        _ = SavePreferencesAsync();
    }

    private async Task SavePreferencesAsync()
    {
        if (_userSettings is null)
        {
            return;
        }

        try
        {
            var settings = _userSettings.Load();
            settings.Theme = ThemeValues[Math.Clamp(SelectedThemeIndex, 0, ThemeValues.Length - 1)];
            settings.Language = LanguageValues[Math.Clamp(SelectedLanguageIndex, 0, LanguageValues.Length - 1)];
            await _userSettings.SaveAsync(settings);
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się zapisać: {0}", ex.Message);
        }
    }
    public string LibrarySummary { get; }
    public string AboutText { get; }

    public ObservableCollection<LibraryFolderDisplayItem> LibraryFolders { get; } = [];

    [ObservableProperty]
    public partial LibraryFolderDisplayItem? SelectedLibraryFolder { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public void RefreshAccountStatus()
    {
        if (_auth?.IsAvailable != true)
        {
            AccountStatus = _accountStatusFallback;
            return;
        }

        if (_auth.IsLoggedIn && _auth.CurrentUser is not null)
        {
            var user = _auth.CurrentUser;
            var email = string.IsNullOrWhiteSpace(user.Email) ? "(brak e-maila w tokenie)" : user.Email;
            AccountStatus = user.IsAdmin
                ? Loc.F("Zalogowano jako: {0} · admin", email)
                : Loc.F("Zalogowano jako: {0}", email);
            return;
        }

        AccountStatus = Loc.T("Niezalogowany — zaloguj się w Profilu");
    }

    public void ReloadFolders()
    {
        LibraryFolders.Clear();
        foreach (var folder in _libraryFolders?.GetFolders() ?? [])
        {
            LibraryFolders.Add(LibraryFolderDisplayItem.FromEntry(folder));
        }
    }

    public async Task AddLibraryFolderAsync(string folderPath)
    {
        if (_libraryFolders is null || _scopes is null || string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _libraryFolders.AddFolderAsync(folderPath);
            ReloadFolders();

            using var scope = _scopes.CreateScope();
            var discovery = scope.ServiceProvider.GetRequiredService<ILibraryDiscoveryService>();
            var result = await discovery.ScanAsync();

            StatusText = result.DiscoveredInstalls + result.UpdatedEntries > 0
                ? Loc.F("Dodano folder biblioteki. Znaleziono {0} gier, zaktualizowano {1}.", result.DiscoveredInstalls, result.UpdatedEntries)
                : Loc.T("Dodano folder biblioteki.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się dodać folderu biblioteki: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedLibraryFolderAsync()
    {
        if (_libraryFolders is null || SelectedLibraryFolder is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _libraryFolders.RemoveFolderAsync(SelectedLibraryFolder.Path);
            ReloadFolders();
            SelectedLibraryFolder = null;
            StatusText = Loc.T("Usunięto folder z listy bibliotek klienta. Pliki gier na dysku pozostały.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się usunąć folderu z listy: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenSelectedLibraryFolder()
    {
        OpenFolder(SelectedLibraryFolder?.Path);
    }

    [RelayCommand]
    private async Task RescanLibraryAsync()
    {
        if (_scopes is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var discovery = scope.ServiceProvider.GetRequiredService<ILibraryDiscoveryService>();
            var result = await discovery.ScanAsync();

            StatusText = Loc.F("Skan zakończony: {0} nowych, {1} zaktualizowanych, {2} oznaczonych jako niezainstalowane.", result.DiscoveredInstalls, result.UpdatedEntries, result.MarkedNotInstalled);
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Skan biblioteki nie powiodł się: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenFolder(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                StatusText = Loc.T("Wybierz folder biblioteki z listy.");
                return;
            }

            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            StatusText = Loc.T("Otwarto folder biblioteki.");
        }
        catch (Exception ex)
        {
            StatusText = Loc.F("Nie udało się otworzyć folderu: {0}", ex.Message);
        }
    }

    private static string ReadVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return asm.GetName().Version?.ToString() ?? "4.0.0.0";
    }
}

public sealed class LibraryFolderDisplayItem
{
    public required string Path { get; init; }
    public required string ContentId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string DisplayText { get; init; } = string.Empty;

    public static LibraryFolderDisplayItem FromEntry(SgsLibraryFolderEntry entry)
    {
        var label = string.IsNullOrWhiteSpace(entry.Label) ? entry.Path : $"{entry.Label} — {entry.Path}";
        return new LibraryFolderDisplayItem
        {
            Path = entry.Path,
            ContentId = entry.ContentId,
            Label = entry.Label ?? string.Empty,
            DisplayText = label
        };
    }
}
