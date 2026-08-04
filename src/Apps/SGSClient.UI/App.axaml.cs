using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client;
using SGSClient.Infrastructure.Client.Persistence;
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
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "SGS_")
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddClientInfrastructure(configuration);

        services.AddTransient<HomeViewModel>();
        services.AddTransient(sp => new GamesViewModel(
            sp.GetService<ISgsApiService>(),
            sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddTransient(sp => new LibraryViewModel(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetService<IGameLauncherService>()));
        services.AddTransient<SettingsViewModel>();
        services.AddTransient(sp => new AccountViewModel(
            sp.GetService<IAuthSession>(),
            sp.GetService<IUserApiClient>(),
            sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddTransient(sp => new MainWindowViewModel(
            sp.GetRequiredService<HomeViewModel>(),
            sp.GetRequiredService<GamesViewModel>(),
            sp.GetRequiredService<LibraryViewModel>(),
            sp.GetRequiredService<SettingsViewModel>(),
            sp.GetRequiredService<AccountViewModel>()));

        Services = services.BuildServiceProvider();

        SgsClientDbInitializer.InitializeAsync(Services).GetAwaiter().GetResult();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };

            _ = vm.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
