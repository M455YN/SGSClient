using Microsoft.EntityFrameworkCore;
using SGSClient.Infrastructure.Client.Persistence.Entities;

namespace SGSClient.Infrastructure.Client.Persistence;

public sealed class SgsClientDbContext(DbContextOptions<SgsClientDbContext> options) : DbContext(options)
{
    public DbSet<LocalInstallEntity> Installs => Set<LocalInstallEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalInstallEntity>(entity =>
        {
            entity.ToTable("local_installs");
            entity.HasKey(x => x.BuildId);
            entity.Property(x => x.Symbol).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Platform).HasConversion<int>();
            entity.Property(x => x.Architecture).HasMaxLength(32);
            entity.Property(x => x.InstallPath).HasMaxLength(1024);
            entity.Property(x => x.ExecutablePath).HasMaxLength(1024);
            entity.Property(x => x.InstalledVersion).HasMaxLength(64);
            entity.Property(x => x.TargetVersion).HasMaxLength(64);
            entity.Property(x => x.CoverUrl).HasMaxLength(2048);
            entity.Property(x => x.CustomCoverPath).HasMaxLength(2048);
            entity.Property(x => x.InstallState).HasConversion<int>();
            entity.HasIndex(x => x.GameId);
            entity.HasIndex(x => x.Symbol);
            entity.HasIndex(x => new { x.GameId, x.Platform, x.Architecture }).IsUnique();
        });
    }
}
