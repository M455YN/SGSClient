using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client;

namespace SGSClient.UI.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory? _scopes;
    private readonly string _dataFolder;

    public SettingsViewModel()
    {
        Title = "Ustawienia";
        StoreStatus = "Nie skonfigurowano";
        AccountStatus = "Logowanie wyłączone";
        AppearanceText = "Jak w systemie Windows / macOS / Linux";
        LanguageText = "Polski";
        LibrarySummary = "Gry dodane ze sklepu są zapisywane na tym komputerze.";
        AboutText = $"SGSClient · {ReadVersion()}";
        _dataFolder = Path.GetDirectoryName(DependencyInjection.GetDefaultLibraryDbPath()) ?? string.Empty;
    }

    public SettingsViewModel(IConfiguration configuration, IServiceScopeFactory scopes)
    {
        _scopes = scopes;
        Title = "Ustawienia";

        var api = configuration.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()
                  ?? new ApiClientSettings();
        var azure = configuration.GetSection(AzureAdSettings.SectionName).Get<AzureAdSettings>()
                    ?? new AzureAdSettings();

        StoreStatus = string.IsNullOrWhiteSpace(api.BaseUrl)
            ? "Sklep niedostępny — brak adresu serwera"
            : "Sklep skonfigurowany";
        AccountStatus = azure.IsConfigured
            ? "Możesz zalogować się w zakładce Profil"
            : azure.Enabled
                ? "Logowanie wymaga dokończenia konfiguracji"
                : "Logowanie jest wyłączone";
        AppearanceText = "Jak w systemie";
        LanguageText = "Polski";
        LibrarySummary = "Gry dodane ze sklepu są zapisywane na tym komputerze. Możesz otworzyć folder z danymi albo wyczyścić listę.";
        AboutText = $"SGSClient · wersja {ReadVersion()}";
        _dataFolder = Path.GetDirectoryName(DependencyInjection.GetDefaultLibraryDbPath()) ?? string.Empty;
    }

    public string Title { get; }
    public string StoreStatus { get; }
    public string AccountStatus { get; }
    public string AppearanceText { get; }
    public string LanguageText { get; }
    public string LibrarySummary { get; }
    public string AboutText { get; }

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_dataFolder))
            {
                StatusText = "Nie znaleziono folderu z danymi.";
                return;
            }

            Directory.CreateDirectory(_dataFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = _dataFolder,
                UseShellExecute = true
            });
            StatusText = "Otwarto folder z Twoimi danymi.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się otworzyć folderu: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClearLibraryAsync()
    {
        if (_scopes is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            using var scope = _scopes.CreateScope();
            var library = scope.ServiceProvider.GetRequiredService<ILocalLibraryStore>();
            var items = await library.GetAllAsync();
            foreach (var item in items)
            {
                await library.RemoveAsync(item.GameId);
            }

            StatusText = items.Count == 0
                ? "Biblioteka jest już pusta."
                : $"Usunięto {items.Count} {(items.Count == 1 ? "grę" : "gier")} z biblioteki na tym komputerze.";
        }
        catch (Exception ex)
        {
            StatusText = $"Nie udało się wyczyścić biblioteki: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string ReadVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var version = asm.GetName().Version;
        if (version is null)
        {
            return "dev";
        }

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
