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
    private readonly string _accountStatusFallback;

    /// <summary>Design-time / fallback constructor (no services).</summary>
    public SettingsViewModel()
    {
        Title = "Ustawienia";
        StoreStatus = "Nie skonfigurowano";
        _accountStatusFallback = "Logowanie wyłączone";
        AccountStatus = _accountStatusFallback;
        AppearanceText = "Jak w systemie";
        LanguageText = "Polski";
        LibrarySummary = "Dodaj foldery biblioteki jak w Steam. Każdy folder ma plik libraryfolder.json i katalog sgsapps/.";
        AboutText = $"SGSClient · wersja {ReadVersion()}";
    }

    public SettingsViewModel(
        IConfiguration configuration,
        IServiceScopeFactory scopes,
        ILibraryFolderService libraryFolders,
        IAuthSession? auth = null)
    {
        _scopes = scopes;
        _libraryFolders = libraryFolders;
        _auth = auth;
        Title = "Ustawienia";

        var api = configuration.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()
                  ?? new ApiClientSettings();
        var azure = configuration.GetSection(AzureAdSettings.SectionName).Get<AzureAdSettings>()
                    ?? new AzureAdSettings();

        StoreStatus = string.IsNullOrWhiteSpace(api.BaseUrl)
            ? "Sklep niedostępny — brak adresu serwera"
            : $"Sklep: {api.BaseUrl.TrimEnd('/')}";
        _accountStatusFallback = azure.IsConfigured
            ? "Możesz zalogować się w zakładce Profil"
            : azure.Enabled
                ? "Logowanie wymaga dokończenia konfiguracji"
                : "Logowanie jest wyłączone";
        AccountStatus = _accountStatusFallback;
        AppearanceText = "Jak w systemie";
        LanguageText = "Polski";
        LibrarySummary = "Dodaj foldery biblioteki jak w Steam. Każdy folder ma plik libraryfolder.json i katalog sgsapps/.";
        AboutText = $"SGSClient · wersja {ReadVersion()}";
        ReloadFolders();
        RefreshAccountStatus();
    }

    public string Title { get; }
    public string StoreStatus { get; }

    [ObservableProperty]
    public partial string AccountStatus { get; set; } = string.Empty;

    public string AppearanceText { get; }
    public string LanguageText { get; }
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
                ? $"Zalogowano jako: {email} · admin"
                : $"Zalogowano jako: {email}";
            return;
        }

        AccountStatus = "Niezalogowany — zaloguj się w Profilu";
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
                ? $"Dodano folder biblioteki. Znaleziono {result.DiscoveredInstalls} gier, zaktualizowano {result.UpdatedEntries}."
                : "Dodano folder biblioteki.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się dodać folderu biblioteki: {ex.Message}";
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
            StatusText = "Usunięto folder z listy bibliotek klienta. Pliki gier na dysku pozostały.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się usunąć folderu z listy: {ex.Message}";
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

            StatusText = $"Skan zakończony: {result.DiscoveredInstalls} nowych, {result.UpdatedEntries} zaktualizowanych, {result.MarkedNotInstalled} oznaczonych jako niezainstalowane.";
        }
        catch (Exception ex)
        {
            StatusText = $"Skan biblioteki nie powiodł się: {ex.Message}";
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
                StatusText = "Wybierz folder biblioteki z listy.";
                return;
            }

            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            StatusText = "Otwarto folder biblioteki.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się otworzyć folderu: {ex.Message}";
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
