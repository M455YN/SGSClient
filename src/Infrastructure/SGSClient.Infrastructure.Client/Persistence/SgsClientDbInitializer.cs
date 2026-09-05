using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SGSClient.Infrastructure.Client.Persistence;

public static class SgsClientDbInitializer
{
    public static async Task InitializeAsync(SgsClientDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await ApplyAdditiveMigrationsAsync(db, cancellationToken);

        if (!await HasRequiredSchemaAsync(db, cancellationToken))
        {
            await RecreateDatabaseAsync(db, cancellationToken);
        }
    }

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgsClientDbContext>();
        await InitializeAsync(db, cancellationToken);
    }

    private static async Task ApplyAdditiveMigrationsAsync(
        SgsClientDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, "local_installs", cancellationToken))
        {
            return;
        }

        if (!await ColumnExistsAsync(db, "local_installs", "TargetVersion", cancellationToken))
        {
            await ExecuteNonQueryAsync(
                db,
                "ALTER TABLE local_installs ADD COLUMN TargetVersion TEXT NULL;",
                cancellationToken);
        }

        if (!await ColumnExistsAsync(db, "local_installs", "CoverUrl", cancellationToken))
        {
            await ExecuteNonQueryAsync(
                db,
                "ALTER TABLE local_installs ADD COLUMN CoverUrl TEXT NULL;",
                cancellationToken);
        }

        if (!await ColumnExistsAsync(db, "local_installs", "LogoUrl", cancellationToken))
        {
            await ExecuteNonQueryAsync(
                db,
                "ALTER TABLE local_installs ADD COLUMN LogoUrl TEXT NULL;",
                cancellationToken);
        }

        if (!await ColumnExistsAsync(db, "local_installs", "CustomCoverPath", cancellationToken))
        {
            await ExecuteNonQueryAsync(
                db,
                "ALTER TABLE local_installs ADD COLUMN CustomCoverPath TEXT NULL;",
                cancellationToken);
        }

        if (!await TableExistsAsync(db, "local_playtime", cancellationToken))
        {
            await ExecuteNonQueryAsync(
                db,
                """
                CREATE TABLE local_playtime (
                    UserId INTEGER NOT NULL,
                    GameId INTEGER NOT NULL,
                    TotalSeconds INTEGER NOT NULL DEFAULT 0,
                    UnsyncedSeconds INTEGER NOT NULL DEFAULT 0,
                    LastPlayedUtc TEXT NULL,
                    SessionCount INTEGER NOT NULL DEFAULT 0,
                    PRIMARY KEY (UserId, GameId)
                );
                CREATE INDEX IX_local_playtime_GameId ON local_playtime (GameId);
                """,
                cancellationToken);
        }
    }

    private static async Task<bool> HasRequiredSchemaAsync(
        SgsClientDbContext db,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await TableExistsAsync(db, "local_installs", cancellationToken))
            {
                return true;
            }

            return await ColumnExistsAsync(db, "local_installs", "BuildId", cancellationToken);
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static async Task RecreateDatabaseAsync(
        SgsClientDbContext db,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Closed)
        {
            await connection.CloseAsync();
        }

        SqliteConnection.ClearAllPools();

        await db.Database.EnsureDeletedAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(
        SgsClientDbContext db,
        string tableName,
        CancellationToken cancellationToken)
    {
        var count = await ExecuteScalarAsync(
            db,
            """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'table' AND name = $name;
            """,
            cancellationToken,
            ("$name", tableName));
        return count > 0;
    }

    private static async Task<bool> ColumnExistsAsync(
        SgsClientDbContext db,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        // pragma_table_info does not accept bound parameters for table name.
        var safeTable = tableName.Replace("'", "''", StringComparison.Ordinal);
        var count = await ExecuteScalarAsync(
            db,
            $"""
            SELECT COUNT(1)
            FROM pragma_table_info('{safeTable}')
            WHERE name = $name;
            """,
            cancellationToken,
            ("$name", columnName));
        return count > 0;
    }

    private static async Task ExecuteNonQueryAsync(
        SgsClientDbContext db,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ExecuteScalarAsync(
        SgsClientDbContext db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
}
