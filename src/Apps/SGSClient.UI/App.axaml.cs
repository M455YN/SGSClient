using Avalonia;
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

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
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
            sp.GetRequiredService<PublishViewModel>(),
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
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };
            // InitializeAsync runs from MainWindow.Opened (needs window handle for MSAL).
        }

        base.OnFrameworkInitializationCompleted();
    }
}
