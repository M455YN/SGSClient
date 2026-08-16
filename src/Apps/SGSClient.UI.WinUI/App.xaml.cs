using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Services;
using SGSClient.UI.WinUI.ViewModels;

namespace SGSClient.UI.WinUI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
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
        services.AddClientInfrastructure(configuration);
        services.AddTransient(sp => new HomeViewModel(sp.GetService<ISgsApiService>()));
        services.AddTransient(sp => new GamesViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration));
        services.AddTransient(sp => new GameDetailsViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration));
        services.AddTransient(sp => new LibraryViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<IGameLauncherService>(),
            sp.GetService<IGameInstallService>(),
            sp.GetService<ILibraryDiscoveryService>(),
            sp.GetService<ISgsApiService>(),
            configuration));
        services.AddTransient(sp => new GamePropertiesViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddTransient(sp => new PublishViewModel(
            sp.GetService<IGamePackagePacker>(),
            sp.GetService<IGameUploadClient>(),
            sp.GetService<IAuthSession>(),
            sp.GetService<ISgsApiService>()));
        services.AddTransient(sp => new StudioViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetService<IAuthSession>()));
        services.AddTransient(sp => new AdminViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetService<IAuthSession>()));
        services.AddTransient(sp => new SettingsViewModel(
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILibraryFolderService>(),
            sp.GetService<IAuthSession>()));
        services.AddTransient(sp => new AccountViewModel(
            sp.GetService<IAuthSession>(),
            sp.GetService<IUserApiClient>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            configuration));
        services.AddSingleton(sp => new ShellSidebarViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<IGameLauncherService>(),
            sp.GetService<ILibraryDiscoveryService>(),
            sp.GetService<IAuthSession>(),
            configuration));

        Services = services.BuildServiceProvider();
        SgsClientDbInitializer.InitializeAsync(Services).GetAwaiter().GetResult();

        Services.GetRequiredService<ILibraryFolderService>().EnsureInitializedAsync().GetAwaiter().GetResult();

        using (var scope = Services.CreateScope())
        {
            var discovery = scope.ServiceProvider.GetService<ILibraryDiscoveryService>();
            discovery?.ScanAsync().GetAwaiter().GetResult();
        }

        MainWindow = new MainWindow();
        MainWindow.SystemBackdrop = new MicaBackdrop();
        MainWindow.Activate();

        var auth = Services.GetService<IAuthSession>();
        if (auth?.IsAvailable != true)
        {
            MainWindow.SetAdminNavVisible(true);
        }

        _ = WarmSilentLoginAsync();
    }

    private static async Task WarmSilentLoginAsync()
    {
        var auth = Services.GetService<IAuthSession>();
        if (auth?.IsAvailable == true)
        {
            await auth.TrySilentLoginAsync();
            if (auth.IsLoggedIn)
            {
                var users = Services.GetService<IUserApiClient>();
                var profile = users is null ? auth.CurrentUser : await users.GetMeAsync();
                if (profile is not null)
                {
                    auth.SetCurrentUser(profile);
                    MainWindow?.SetAdminNavVisible(profile.IsAdmin);
                    MainWindow?.RefreshSidebar();
                }
            }
        }
    }
}
