using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Interfaces;
using SGSClient.Core.Localization;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Services;
using SGSClient.UI.ViewModels;
using SGSClient.UI.Views;

namespace SGSClient.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static Action? _exit;

    /// <summary>Really quits: closing the window only hides it to the tray.</summary>
    public static void ExitApplication() => _exit?.Invoke();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Show the logo first and load the client on a later dispatcher turn so the splash can paint.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime startup)
        {
            var splash = new SplashWindow();
            startup.MainWindow = splash;
            splash.Show();
            Avalonia.Threading.Dispatcher.UIThread.Post(
                () =>
                {
                    try
                    {
                        StartClient();
                    }
                    finally
                    {
                        splash.Close();
                    }
                },
                Avalonia.Threading.DispatcherPriority.Background);
        }
        else
        {
            StartClient();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartClient()
    {
        var userSettings = new ClientUserSettingsStore().Load();
        Loc.Initialize(userSettings.Language);
        RequestedThemeVariant = AvaloniaThemeApplier.ToVariant(userSettings.Theme);

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
        services.AddSingleton<IThemeApplier, AvaloniaThemeApplier>();
        services.AddClientInfrastructure(configuration);

        services.AddTransient(sp => new HomeViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<IAuthSession>(),
            configuration,
            sp.GetService<ICatalogCacheStore>()));
        services.AddTransient(sp => new GamesViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration: configuration,
            auth: sp.GetService<IAuthSession>(),
            catalogCache: sp.GetService<ICatalogCacheStore>()));
        services.AddTransient(sp => new GameDetailsViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration: configuration,
            queue: sp.GetService<IInstallQueueService>(),
            auth: sp.GetService<IAuthSession>()));
        services.AddTransient(sp => new LibraryViewModel(
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
        services.AddTransient(sp => new WishlistViewModel(
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
            sp.GetService<IThemeApplier>()));
        services.AddTransient(sp => new AccountViewModel(
            sp.GetService<IAuthSession>(),
            sp.GetService<IUserApiClient>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration));
        services.AddSingleton(sp => new LoginGateViewModel(
            sp.GetService<IAuthSession>(),
            sp.GetService<IUserApiClient>()));
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
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
        services.AddTransient(sp => new MainWindowViewModel(
            sp.GetRequiredService<HomeViewModel>(),
            sp.GetRequiredService<GamesViewModel>(),
            sp.GetRequiredService<GameDetailsViewModel>(),
            sp.GetRequiredService<LibraryViewModel>(),
            sp.GetRequiredService<DownloadsViewModel>(),
            sp.GetRequiredService<WishlistViewModel>(),
            sp.GetRequiredService<CreativeViewModel>(),
            sp.GetRequiredService<PublishViewModel>(),
            sp.GetRequiredService<AddMediaViewModel>(),
            sp.GetRequiredService<AddStoryViewModel>(),
            sp.GetRequiredService<StudioViewModel>(),
            sp.GetRequiredService<AdminViewModel>(),
            sp.GetRequiredService<SettingsViewModel>(),
            sp.GetRequiredService<AccountViewModel>(),
            sp.GetRequiredService<FriendsViewModel>(),
            sp.GetRequiredService<ShellSidebarViewModel>(),
            sp.GetRequiredService<LoginGateViewModel>(),
            sp.GetService<INetworkStatusService>()));

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            vm.Games.SetOpenAccountHandler(() => vm.NavigateCommand.Execute("Account"));
            vm.GameDetails.SetOpenAccountHandler(() => vm.NavigateCommand.Execute("Account"));
            vm.LoginGate.SetParentWindowHandle(() =>
                desktop.MainWindow?.TryGetPlatformHandle()?.Handle ?? 0);
            var window = new MainWindow
            {
                DataContext = vm,
            };
            desktop.MainWindow = window;
            window.Show();
            SetUpTray(desktop, window, vm);
            // InitializeAsync runs from MainWindow.Opened (needs window handle for MSAL).
        }

    }

    /// <summary>Closing the window hides it to the tray; "Exit" in the tray menu really quits.</summary>
    private void SetUpTray(IClassicDesktopStyleApplicationLifetime desktop, Window window, MainWindowViewModel vm)
    {
        var exiting = false;
        _exit = () =>
        {
            exiting = true;
            desktop.Shutdown();
        };
        window.Closing += (_, e) =>
        {
            if (!exiting)
            {
                e.Cancel = true;
                window.Hide();
            }
        };

        void Show()
        {
            window.Show();
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
        }

        void ShowAndNavigate(string tag)
        {
            Show();
            vm.NavigateCommand.Execute(tag);
        }

        NativeMenuItem Item(string text, Action click)
        {
            var item = new NativeMenuItem(text);
            item.Click += (_, _) => click();
            return item;
        }

        var menu = new NativeMenu();
        void Rebuild()
        {
            menu.Items.Clear();
            var games = vm.Sidebar.InstalledGames.Where(g => g.CanLaunch)
                .OrderByDescending(g => g.LastPlayedUtc ?? DateTimeOffset.MinValue).Take(5).ToList();
            foreach (var game in games)
            {
                var captured = game;
                menu.Items.Add(Item(captured.Title, () => vm.Sidebar.LaunchCommand.Execute(captured)));
            }

            if (games.Count > 0)
            {
                menu.Items.Add(new NativeMenuItemSeparator());
            }

            menu.Items.Add(Item(Loc.T("Sklep"), () => ShowAndNavigate("Games")));
            menu.Items.Add(Item(Loc.T("Biblioteka"), () => ShowAndNavigate("Library")));
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(Item(Loc.T("Znajomi"), () => ShowAndNavigate("Friends")));
            menu.Items.Add(Item(Loc.T("Ustawienia"), () => ShowAndNavigate("Settings")));
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(Item(Loc.T("Wyjdź z SGSClient"), ExitApplication));
        }

        // Pre-populated: with the window hidden the menu can show before Opening has filled it.
        Rebuild();
        menu.Opening += (_, _) => Rebuild();

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://SGSClient/Assets/app.ico"))),
            ToolTipText = "SGSClient",
            Menu = menu
        };
        tray.Clicked += (_, _) => Show();
        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }
}
