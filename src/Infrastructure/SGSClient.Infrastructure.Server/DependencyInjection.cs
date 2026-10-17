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
        services.Configure<AchievementSettings>(configuration.GetSection(AchievementSettings.SectionName));
        services.AddDbContext<SgsServerDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddScoped<IUserService, EfUserService>();
        services.AddScoped<IFriendsService, FriendsService>();
        services.AddScoped<IPlaytimeService, PlaytimeService>();
        services.AddScoped<IEntitlementService, EfEntitlementService>();
        services.AddScoped<IAchievementService, EfAchievementService>();
        services.AddSingleton<IAchievementTokenService, AchievementTokenService>();
        services.AddScoped<IWishlistService, EfWishlistService>();
        services.AddScoped<IGameReviewService, EfGameReviewService>();
        services.AddScoped<IProfileCommentService, EfProfileCommentService>();
        services.AddScoped<IChatService, EfChatService>();
        services.AddScoped<INotificationService, EfNotificationService>();
        services.AddSingleton<IAdminAuthorization, AdminAuthorization>();
        services.AddScoped<GameBuildVersionService>();
        services.AddScoped<CreativeStorage>();
        services.AddScoped<IArtworkService, EfArtworkService>();
        services.AddScoped<IMusicService, EfMusicService>();
        services.AddScoped<IStoryService, EfStoryService>();
        services.AddScoped<ICreativeCommentService, EfCreativeCommentService>();
        services.AddScoped<IGamePackagePublisher, GamePackagePublisher>();
        services.AddScoped<IGameMediaService, GameMediaService>();
        services.AddScoped<IUserAvatarService, UserAvatarService>();
        services.AddSingleton<IRofwLegacyService, RofwLegacyService>();
        services.AddSingleton<IReportingoLegacyService, ReportingoLegacyService>();

        return services;
    }
}
