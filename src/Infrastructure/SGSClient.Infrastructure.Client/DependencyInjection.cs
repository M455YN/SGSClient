using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Client.Auth;
using SGSClient.Infrastructure.Client.Persistence;
using SGSClient.Infrastructure.Client.Services;

namespace SGSClient.Infrastructure.Client;

public static class DependencyInjection
{
    public static IServiceCollection AddClientInfrastructure(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        string? sqliteDatabasePath = null)
    {
        var dbPath = sqliteDatabasePath ?? GetDefaultLibraryDbPath();
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        services.AddDbContext<SgsClientDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton<IGameLauncherService, ProcessGameLauncherService>();
        services.AddScoped<ILocalLibraryStore, LocalLibraryService>();
        services.AddScoped<IPlaytimeStore>(sp => new LocalPlaytimeStore(
            sp.GetRequiredService<SgsClientDbContext>(),
            sp.GetService<IAuthSession>(),
            sp.GetService<IUserApiClient>()));
        services.AddSingleton<IGamePackagePacker, GamePackagePacker>();
        services.AddSingleton<IClientUserSettingsStore, ClientUserSettingsStore>();
        services.AddSingleton<ILibraryFoldersStore, LibraryFoldersStore>();
        services.AddSingleton<ILibraryFolderService, LibraryFolderService>();
        services.AddSingleton<ILibraryPathProvider, LibraryPathProvider>();
        services.AddScoped<ILibraryDiscoveryService, LibraryDiscoveryService>();
        services.Configure<ClientInstallSettings>(_ => { });
        services.Configure<ApiClientSettings>(_ => { });

        if (configuration is not null)
        {
            services.Configure<AzureAdSettings>(configuration.GetSection(AzureAdSettings.SectionName));
            services.Configure<ApiClientSettings>(configuration.GetSection(ApiClientSettings.SectionName));
            services.Configure<ClientInstallSettings>(configuration.GetSection(ClientInstallSettings.SectionName));

            var apiBase = configuration.GetSection(ApiClientSettings.SectionName).Get<ApiClientSettings>()?.BaseUrl
                ?? new ApiClientSettings().BaseUrl;

            services.AddHttpClient<IUserApiClient, BearerUserApiClient>(client =>
            {
                client.BaseAddress = new Uri(apiBase);
            });

            services.AddHttpClient<ISgsApiService, HttpSgsApiService>(client =>
            {
                client.BaseAddress = new Uri(apiBase);
            });

            services.AddHttpClient<IGameUploadClient, GameUploadClient>(client =>
            {
                client.BaseAddress = new Uri(apiBase);
                client.Timeout = TimeSpan.FromMinutes(30);
            });

            services.AddHttpClient<IGameInstallService, GameInstallService>(client =>
            {
                client.BaseAddress = new Uri(apiBase);
                client.Timeout = TimeSpan.FromMinutes(60);
            });

            services.AddSingleton<IInstallQueueService, InstallQueueService>();
            services.AddSingleton<PresenceHeartbeatService>();

            services.AddSingleton<IAuthSession>(sp =>
                new MsalAuthSession(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AzureAdSettings>>(),
                    () => sp.GetRequiredService<IUserApiClient>()));
        }

        return services;
    }

    public static string GetDefaultLibraryDbPath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SGSClient");
        return Path.Combine(root, "library.db");
    }
}
