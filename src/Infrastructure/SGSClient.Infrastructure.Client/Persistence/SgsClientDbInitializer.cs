using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SGSClient.Infrastructure.Client.Persistence;

public static class SgsClientDbInitializer
{
    public static async Task InitializeAsync(SgsClientDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await HasCompatibleSchemaAsync(db, cancellationToken))
        {
            await db.Database.EnsureDeletedAsync(cancellationToken);
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
    }

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
        await InitializeAsync(db, cancellationToken);
    }

    private static async Task<bool> HasCompatibleSchemaAsync(
        SgsClientDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table' AND name = 'local_installs';
                """;
            var tableExists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!tableExists)
            {
                return true;
            }

            command.CommandText =
                """
                SELECT COUNT(1)
                FROM pragma_table_info('local_installs')
                WHERE name = 'BuildId';
                """;
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        }
        catch (SqliteException)
        {
            return false;
        }
    }
}
