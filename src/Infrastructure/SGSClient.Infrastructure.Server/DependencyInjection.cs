using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Configuration;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Repositories;
using SGSClient.Infrastructure.Server.Persistence.Services;
using SGSClient.Infrastructure.Server.Services;

namespace SGSClient.Infrastructure.Server;

public static class DependencyInjection
{
    public static IServiceCollection AddServerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SgsDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'SgsDatabase' is missing. Set ConnectionStrings:SgsDatabase in appsettings.");

        services.Configure<GameStorageSettings>(configuration.GetSection(GameStorageSettings.SectionName));
        services.Configure<AdminSettings>(configuration.GetSection(AdminSettings.SectionName));
        services.AddDbContext<SgsServerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddScoped<IUserService, EfUserService>();
        services.AddSingleton<IAdminAuthorization, AdminAuthorization>();
        services.AddScoped<GameBuildVersionService>();
        services.AddScoped<IGamePackagePublisher, GamePackagePublisher>();
        services.AddSingleton<IRofwLegacyService, RofwLegacyService>();
        services.AddSingleton<IReportingoLegacyService, ReportingoLegacyService>();

        return services;
    }
}
