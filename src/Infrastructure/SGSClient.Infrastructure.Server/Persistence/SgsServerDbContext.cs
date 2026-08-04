using Microsoft.EntityFrameworkCore;
using SGSClient.Infrastructure.Server.Persistence.Entities;

namespace SGSClient.Infrastructure.Server.Persistence;

public sealed class SgsServerDbContext(DbContextOptions<SgsServerDbContext> options) : DbContext(options)
{
    public DbSet<GameEntity> Games => Set<GameEntity>();
    public DbSet<GameBuildEntity> GameBuilds => Set<GameBuildEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();

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
            entity.Property(x => x.SettingsJson).HasMaxLength(8000).IsRequired();
            entity.HasIndex(x => x.AzureSub).IsUnique();
            entity.HasIndex(x => x.Email);
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

            entity.HasIndex(x => x.Symbol).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.DraftP);
            entity.HasIndex(x => x.FeaturedP);

            entity.HasMany(x => x.Builds)
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

            entity.HasIndex(x => new { x.GameId, x.Platform, x.Architecture }).IsUnique();
            entity.HasIndex(x => new { x.GameId, x.IsDefault });
        });
    }
}
