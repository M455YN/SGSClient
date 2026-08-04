using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SGSClient.Infrastructure.Server.Persistence;

public sealed class SgsServerDbContextFactory : IDesignTimeDbContextFactory<SgsServerDbContext>
{
    public SgsServerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SgsServerDbContext>()
            .UseSqlServer(
                "Data Source=localhost;Initial Catalog=sgsclient;Integrated Security=True;TrustServerCertificate=True")
            .Options;

        return new SgsServerDbContext(options);
    }
}
