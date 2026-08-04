using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSClient.Core.Interfaces;
using SGSClient.Infrastructure.Server.Persistence;
using SGSClient.Infrastructure.Server.Persistence.Repositories;
using SGSClient.Infrastructure.Server.Persistence.Services;

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

        services.AddDbContext<SgsServerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddScoped<IUserService, EfUserService>();

        return services;
    }
}
