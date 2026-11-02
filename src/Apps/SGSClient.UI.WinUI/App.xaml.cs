using WinUIEx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Services;
using SGSClient.UI.ViewModels;
using SGSClient.UI.WinUI.Views;

namespace SGSClient.UI.WinUI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow? MainWindow { get; private set; }

    private SimpleSplashScreen? _splash;

    public App()
    {
        // The logo goes up before anything else loads and is hidden once the shell window is active.
        _splash = SimpleSplashScreen.ShowSplashScreenImage(
            Path.Combine(AppContext.BaseDirectory, "Assets", "Splash.png"));
        UnhandledException += (_, e) =>
        {
            LogStartupFailure("UnhandledException", e.Exception);
            e.Handled = true;
            ShowErrorDialog(e.Exception);
        };
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            LaunchCore();
        }
        catch (Exception ex)
        {
            LogStartupFailure("OnLaunched", ex);
            throw;
        }
        finally
        {
            _splash?.Hide();
            _splash = null;
        }
    }

    private void LaunchCore()
    {
        var userSettings = new ClientUserSettingsStore().Load();
        Loc.Initialize(userSettings.Language);

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "SGS_")
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IThemeApplier, WinUiThemeApplier>();
        services.AddClientInfrastructure(configuration);
        services.AddTransient(sp => new HomeViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<IAuthSession>(),
            configuration,
            sp.GetService<ICatalogCacheStore>()));
        services.AddSingleton(sp => new GamesViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration: configuration,
            auth: sp.GetService<IAuthSession>(),
            catalogCache: sp.GetService<ICatalogCacheStore>()));
        services.AddSingleton(sp => new GameDetailsViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration: configuration,
            queue: sp.GetService<IInstallQueueService>(),
            auth: sp.GetService<IAuthSession>()));
        // Do not resolve IGameInstallService from the root provider (it needs scoped ILocalLibraryStore).
        services.AddSingleton(sp => new LibraryViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<IGameLauncherService>(),
            installer: null,
            sp.GetService<ILibraryDiscoveryService>(),
            sp.GetService<ISgsApiService>(),
            configuration,
            sp.GetService<IInstallQueueService>(),
            sp.GetService<IAuthSession>()));
        services.AddSingleton(sp => new DownloadsViewModel(
            sp.GetService<IInstallQueueService>(),
            sp.GetService<IClientUserSettingsStore>()));
        services.AddSingleton(sp => new WishlistViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            sp.GetService<IAuthSession>(),
            sp.GetService<ICatalogCacheStore>()));
        services.AddTransient(sp => new GamePropertiesViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<IInstallQueueService>(),
            sp.GetService<IClientUserSettingsStore>()));
        services.AddTransient(sp => new PublishViewModel(
            sp.GetService<IGamePackagePacker>(),
            sp.GetService<IGameUploadClient>(),
            sp.GetService<IAuthSession>(),
            sp.GetService<ISgsApiService>()));
        services.AddTransient(sp => new AddMediaViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetService<IAuthSession>()));
        services.AddTransient(sp => new AddStoryViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetService<IAuthSession>()));
        services.AddTransient(sp =>
        {
            var apiBase = configuration?.GetSection(SGSClient.Core.Configuration.ApiClientSettings.SectionName).Get<SGSClient.Core.Configuration.ApiClientSettings>()?.BaseUrl
                ?? new SGSClient.Core.Configuration.ApiClientSettings().BaseUrl;
            var api = sp.GetService<ISgsApiService>();
            return new CreativeViewModel(
                new CreativeArtViewModel(api, apiBase),
                new CreativeMusicViewModel(api, apiBase),
                new CreativeStoriesViewModel(api, apiBase));
        });
        services.AddTransient(sp => new StudioViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetService<IAuthSession>(),
            configuration));
        services.AddTransient(sp => new AdminViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetService<IAuthSession>(),
            configuration));
        services.AddTransient(sp => new SettingsViewModel(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILibraryFolderService>(),
            sp.GetService<IAuthSession>(),
            sp.GetService<IClientUserSettingsStore>(),
            sp.GetService<IThemeApplier>(),
            sp.GetRequiredService<DownloadsViewModel>()));
        services.AddTransient(sp => new AccountViewModel(
            sp.GetService<IAuthSession>(),
            sp.GetService<IUserApiClient>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration));
        services.AddSingleton(sp => new LoginGateViewModel(
            sp.GetService<IAuthSession>(),
            sp.GetService<IUserApiClient>()));
        services.AddSingleton<IUiDispatcher, WinUiDispatcher>();
        services.AddSingleton(sp => new FriendsViewModel(
            sp.GetService<IUserApiClient>(),
            sp.GetService<IAuthSession>(),
            sp.GetService<SocialRealtimeService>(),
            sp.GetService<IUiDispatcher>()));
        services.AddSingleton(sp => new NotificationsViewModel(
            sp.GetService<IUserApiClient>(),
            sp.GetService<IAuthSession>(),
            sp.GetService<SocialRealtimeService>()));
        services.AddSingleton(sp => new ShellSidebarViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<IGameLauncherService>(),
            sp.GetService<ILibraryDiscoveryService>(),
            sp.GetService<IAuthSession>(),
            configuration,
            sp.GetService<NotificationsViewModel>(),
            sp.GetService<ISgsApiService>(),
            sp.GetService<ICatalogCacheStore>(),
            sp.GetService<IUiDispatcher>()));

        Services = services.BuildServiceProvider();
        SgsClientDbInitializer.InitializeAsync(Services).GetAwaiter().GetResult();

        Services.GetRequiredService<ILibraryFolderService>().EnsureInitializedAsync().GetAwaiter().GetResult();
        Services.GetService<IInstallQueueService>()?.RestorePersistedAsync().GetAwaiter().GetResult();
        Services.GetService<PresenceHeartbeatService>()?.Start();
        Services.GetService<AchievementBridge>()?.Start();
        Services.GetService<INetworkStatusService>()?.StartAsync().GetAwaiter().GetResult();

        using (var scope = Services.CreateScope())
        {
            var discovery = scope.ServiceProvider.GetService<ILibraryDiscoveryService>();
            discovery?.ScanAsync().GetAwaiter().GetResult();
        }

        MainWindow = new MainWindow();
        MainWindow.SystemBackdrop = new MicaBackdrop();
        MainWindow.Activate();
        new WinUiThemeApplier().Apply(userSettings.Theme);

        var gamesVm = Services.GetRequiredService<GamesViewModel>();
        var detailsVm = Services.GetRequiredService<GameDetailsViewModel>();
        var wishlistVm = Services.GetRequiredService<WishlistViewModel>();
        var downloadsVm = Services.GetRequiredService<DownloadsViewModel>();
        gamesVm.SetOpenAccountHandler(() => MainWindow?.NavigateTo(typeof(AccountPage), selectTag: "Account"));
        detailsVm.SetOpenAccountHandler(() => MainWindow?.NavigateTo(typeof(AccountPage), selectTag: "Account"));
        wishlistVm.SetOpenAccountHandler(() => MainWindow?.NavigateTo(typeof(AccountPage), selectTag: "Account"));
        wishlistVm.SetOpenDetailsHandler(id =>
            MainWindow?.NavigateTo(typeof(GameDetailsPage), new GameDetailsNavArgs(id, "Wishlist"), selectTag: "Wishlist"));
        downloadsVm.SetOpenDetailsHandler(id =>
            MainWindow?.NavigateTo(typeof(GameDetailsPage), new GameDetailsNavArgs(id, "Downloads"), selectTag: "Downloads"));

        var loginGate = Services.GetRequiredService<LoginGateViewModel>();
        loginGate.SetOnAuthenticated(EnterAuthenticatedShellAsync);

        _ = WarmSilentLoginAsync();
    }

    public static void ReturnToLoginGate()
    {
        var loginGate = Services.GetRequiredService<LoginGateViewModel>();
        loginGate.RequiresLogin = true;
        loginGate.StatusText = string.Empty;
        var sidebar = Services.GetService<ShellSidebarViewModel>();
        _ = sidebar?.Notifications.StopAsync();
        MainWindow?.RefreshSidebar();
        MainWindow?.NavigateTo(typeof(HomePage), selectTag: "Home");
        MainWindow?.ShowLoginGate(loginGate);
    }

    private static async Task EnterAuthenticatedShellAsync()
    {
        MainWindow?.HideLoginGate();
        var auth = Services.GetService<IAuthSession>();
        if (auth?.CurrentUser is not null)
        {
            MainWindow?.SetAdminNavVisible(auth.CurrentUser.IsAdmin);
        }

        MainWindow?.RefreshSidebar();
        var library = Services.GetService<LibraryViewModel>();
        if (library is not null)
        {
            await library.LoadAsync();
            await library.CheckUpdatesOnStartupAsync();
        }

        var account = Services.GetService<AccountViewModel>();
        if (account is not null)
        {
            MainWindow?.AttachAccount(account);
            await account.InitializeAsync();
            if (account.NeedsPlayerTagSetup)
            {
                MainWindow?.NavigateTo(typeof(AccountPage), selectTag: "Account");
            }
        }

        var sidebar = Services.GetService<ShellSidebarViewModel>();
        if (sidebar is not null)
        {
            await sidebar.Notifications.StartAsync();
        }
    }

    private static bool _errorDialogOpen;

    /// <summary>Shows the unhandled exception (full details, copyable) instead of failing silently.</summary>
    private static void ShowErrorDialog(Exception? ex)
    {
        var window = MainWindow;
        if (ex is null || window?.Content?.XamlRoot is not { } xamlRoot || _errorDialogOpen)
        {
            return;
        }

        window.DispatcherQueue.TryEnqueue(async () =>
        {
            _errorDialogOpen = true;
            try
            {
                var details = ex.ToString();
                var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
                {
                    Title = Loc.T("Wystąpił nieoczekiwany błąd"),
                    Content = new Microsoft.UI.Xaml.Controls.ScrollViewer
                    {
                        MaxHeight = 420,
                        Content = new Microsoft.UI.Xaml.Controls.TextBlock
                        {
                            Text = details,
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12,
                            TextWrapping = TextWrapping.Wrap,
                            IsTextSelectionEnabled = true
                        }
                    },
                    PrimaryButtonText = Loc.T("Kopiuj"),
                    CloseButtonText = Loc.T("Zamknij"),
                    DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close,
                    XamlRoot = xamlRoot
                };
                if (await dialog.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    package.SetText(details);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                }
            }
            catch
            {
                // another dialog is already open; the error is in startup-error.log
            }
            finally
            {
                _errorDialogOpen = false;
            }
        });
    }

    private static void LogStartupFailure(string source, Exception? ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SGSClient");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "startup-error.log");
            File.AppendAllText(path, $"[{DateTimeOffset.Now:O}] {source}\n{ex}\n\n");
        }
        catch
        {
            // ignore logging failures
        }
    }

    private static async Task WarmSilentLoginAsync()
    {
        try
        {
            var loginGate = Services.GetRequiredService<LoginGateViewModel>();
            var entered = await loginGate.TrySilentEnterAsync();
            if (!entered)
            {
                MainWindow?.ShowLoginGate(loginGate);
                return;
            }

            await EnterAuthenticatedShellAsync();
        }
        catch (Exception ex)
        {
            LogStartupFailure("WarmSilentLoginAsync", ex);
            var loginGate = Services.GetService<LoginGateViewModel>();
            if (loginGate is not null)
            {
                MainWindow?.ShowLoginGate(loginGate);
            }
        }
    }
}
