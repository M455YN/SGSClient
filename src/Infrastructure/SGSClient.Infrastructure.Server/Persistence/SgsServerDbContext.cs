using Microsoft.EntityFrameworkCore;
using SGSClient.Core.Enums;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence;

public sealed class SgsServerDbContext(DbContextOptions<SgsServerDbContext> options) : DbContext(options)
{
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<GameBuildEntity> GameBuilds => Set<GameBuildEntity>();
    public DbSet<GameBuildVersionEntity> GameBuildVersions => Set<GameBuildVersionEntity>();
    public DbSet<GameGenreEntity> GameGenres => Set<GameGenreEntity>();
    public DbSet<GameEngineEntity> GameEngines => Set<GameEngineEntity>();
    public DbSet<GameScreenshotEntity> GameScreenshots => Set<GameScreenshotEntity>();
    public DbSet<GameDownloadEntity> GameDownloads => Set<GameDownloadEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserRelationshipEntity> UserRelationships => Set<UserRelationshipEntity>();
    public DbSet<UserGamePlaytimeEntity> UserGamePlaytimes => Set<UserGamePlaytimeEntity>();
    public DbSet<UserGameEntitlementEntity> UserGameEntitlements => Set<UserGameEntitlementEntity>();
    public DbSet<UserGameWishlistEntity> UserGameWishlists => Set<UserGameWishlistEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();
            entity.Property(x => x.AzureSub).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.AvatarUrl).HasMaxLength(2048);
            entity.Property(x => x.Bio).HasMaxLength(500);
            entity.Property(x => x.Location).HasMaxLength(128);
            entity.Property(x => x.PlayerTag).HasMaxLength(32);
            entity.Property(x => x.Role).HasConversion<int>().HasDefaultValue(UserRole.User);
            entity.Property(x => x.SettingsJson).HasMaxLength(8000).IsRequired();
            entity.HasIndex(x => x.AzureSub).IsUnique();
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.PlayerTag)
                .IsUnique()
                .HasFilter("[PlayerTag] IS NOT NULL");
            entity.HasIndex(x => x.LastSeenUtc);
        });

        modelBuilder.Entity<UserGameEntitlementEntity>(entity =>
        {
            entity.ToTable("user_game_entitlements");
            entity.HasKey(x => new { x.UserId, x.GameId });
            entity.Property(x => x.Source).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.GameId);
            entity.HasIndex(x => new { x.UserId, x.GrantedAtUtc });

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserGameWishlistEntity>(entity =>
        {
            entity.ToTable("user_game_wishlists");
            entity.HasKey(x => new { x.UserId, x.GameId });
            entity.HasIndex(x => x.GameId);
            entity.HasIndex(x => new { x.UserId, x.AddedAtUtc });

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<UserRelationshipEntity>(entity =>
        {
            entity.ToTable("user_relationships");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();
            entity.Property(x => x.Kind).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.HasIndex(x => new { x.RequesterUserId, x.AddresseeUserId, x.Kind }).IsUnique();
            entity.HasIndex(x => new { x.AddresseeUserId, x.Kind, x.Status });
            entity.HasIndex(x => new { x.RequesterUserId, x.Kind, x.Status });

            entity.HasOne(x => x.Requester)
                .WithMany()
                .HasForeignKey(x => x.RequesterUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Addressee)
                .WithMany()
                .HasForeignKey(x => x.AddresseeUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserGamePlaytimeEntity>(entity =>
        {
            entity.ToTable("user_game_playtime");
            entity.HasKey(x => new { x.UserId, x.GameId });

            entity.HasIndex(x => new { x.UserId, x.LastPlayedUtc });
            entity.HasIndex(x => x.GameId);

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameGenreEntity>(entity =>
        {
            entity.ToTable("game_genres");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();
            entity.Property(x => x.Symbol).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Symbol).IsUnique();
        });

        modelBuilder.Entity<GameEngineEntity>(entity =>
        {
            entity.ToTable("game_engines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();
            entity.Property(x => x.Symbol).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Symbol).IsUnique();
        });

        modelBuilder.Entity<GameEntity>(entity =>
        {
            entity.ToTable("games");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();

            entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Symbol).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(8000);
            entity.Property(x => x.HardwareRequirements).HasMaxLength(8000);
            entity.Property(x => x.OtherInformation).HasMaxLength(8000);
            entity.Property(x => x.CoverUrl).HasMaxLength(2048);
            entity.Property(x => x.HeaderUrl).HasMaxLength(2048);
            entity.Property(x => x.IntroUrl).HasMaxLength(2048);
            entity.Property(x => x.Kind).HasConversion<int>();
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.StudioName).HasMaxLength(128);
            entity.Property(x => x.WebsiteUrl).HasMaxLength(2048);
            entity.Property(x => x.DiscordUrl).HasMaxLength(2048);
            entity.Property(x => x.SupportUrl).HasMaxLength(2048);
            entity.Property(x => x.ReleaseStatus).HasConversion<int>().HasDefaultValue(GameReleaseStatus.Released);
            entity.Property(x => x.AgeRating).HasConversion<int>().HasDefaultValue(GameAgeRating.Unrated);
            entity.Property(x => x.Features).HasConversion<int>().HasDefaultValue(GameFeatures.None);
            entity.Property(x => x.LanguagesCsv).HasMaxLength(512);
            entity.Property(x => x.TagsCsv).HasMaxLength(512);

            entity.HasIndex(x => x.Symbol).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.DraftP);
            entity.HasIndex(x => x.FeaturedP);
            entity.HasIndex(x => x.FeaturedOrder);
            entity.HasIndex(x => x.ParentGameId);
            entity.HasIndex(x => x.Kind);
            entity.HasIndex(x => x.ReleaseStatus);

            entity.HasOne(x => x.ParentGame)
                .WithMany(x => x.Dlcs)
                .HasForeignKey(x => x.ParentGameId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Genre)
                .WithMany(x => x.Games)
                .HasForeignKey(x => x.TypeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Engine)
                .WithMany(x => x.Games)
                .HasForeignKey(x => x.EngineId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(x => x.Builds)
                .WithOne(x => x.Game)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Screenshots)
                .WithOne(x => x.Game)
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameBuildEntity>(entity =>
        {
            entity.ToTable("game_builds");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();

            entity.Property(x => x.Platform).HasConversion<int>();
            entity.Property(x => x.Architecture).HasMaxLength(32);
            entity.Property(x => x.CurrentVersion).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExeName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ZipLink).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.PackageSizeBytes);

            entity.HasIndex(x => new { x.GameId, x.Platform, x.Architecture }).IsUnique();
            entity.HasIndex(x => new { x.GameId, x.IsDefault });

            entity.HasMany(x => x.Versions)
                .WithOne(x => x.GameBuild)
                .HasForeignKey(x => x.GameBuildId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameBuildVersionEntity>(entity =>
        {
            entity.ToTable("game_build_versions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();

            entity.Property(x => x.Version).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ExeName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ZipLink).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ReleaseNotes).HasMaxLength(4000);

            entity.HasIndex(x => new { x.GameBuildId, x.Version }).IsUnique();
            entity.HasIndex(x => new { x.GameBuildId, x.PublishedAtUtc });
        });

        modelBuilder.Entity<GameDownloadEntity>(entity =>
        {
            entity.ToTable("game_downloads");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();
            entity.Property(x => x.Version).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PlatformKey).HasMaxLength(64).IsRequired();

            entity.HasIndex(x => new { x.GameId, x.DownloadedAtUtc });

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GameScreenshotEntity>(entity =>
        {
            entity.ToTable("game_screenshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();
            entity.Property(x => x.Url).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.SourcePath).HasMaxLength(260);
            entity.HasIndex(x => new { x.GameId, x.SortOrder });
        });
    }
}
