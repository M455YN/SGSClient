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
            await ApplyAdditiveMigrationsAsync(db, cancellationToken);
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
            await EnsureInstallQueueTableAsync(db, cancellationToken);
            await EnsureCachedPayloadsTableAsync(db, cancellationToken);
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

        await MigrateLocalInstallsPrimaryKeyAsync(db, cancellationToken);
        await EnsureInstallQueueTableAsync(db, cancellationToken);
        await EnsureCachedPayloadsTableAsync(db, cancellationToken);
    }

    private static async Task EnsureCachedPayloadsTableAsync(
        SgsClientDbContext db,
        CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(db, "cached_payloads", cancellationToken))
        {
            return;
        }

        await ExecuteNonQueryAsync(
            db,
            """
            CREATE TABLE cached_payloads (
                Key TEXT NOT NULL CONSTRAINT PK_cached_payloads PRIMARY KEY,
                PayloadJson TEXT NOT NULL,
                SavedAtUtc TEXT NOT NULL
            );
            """,
            cancellationToken);
    }

    private static async Task MigrateLocalInstallsPrimaryKeyAsync(
        SgsClientDbContext db,
        CancellationToken cancellationToken)
    {
        if (await IsPrimaryKeyAsync(db, "local_installs", "GameId", cancellationToken))
        {
            return;
        }

        // Recreate table with GameId PK, dedupe by GameId preferring Installed (state=4).
        await ExecuteNonQueryAsync(
            db,
            """
            BEGIN;
            CREATE TABLE local_installs_new (
                GameId INTEGER NOT NULL CONSTRAINT PK_local_installs PRIMARY KEY,
                BuildId INTEGER NOT NULL,
                Symbol TEXT NOT NULL,
                Title TEXT NOT NULL,
                Platform INTEGER NOT NULL,
                Architecture TEXT NULL,
                InstallState INTEGER NOT NULL,
                InstallPath TEXT NULL,
                ExecutablePath TEXT NULL,
                InstalledVersion TEXT NULL,
                TargetVersion TEXT NULL,
                CoverUrl TEXT NULL,
                LogoUrl TEXT NULL,
                CustomCoverPath TEXT NULL,
                LastPlayedUtc TEXT NULL
            );
            INSERT INTO local_installs_new (
                GameId, BuildId, Symbol, Title, Platform, Architecture, InstallState,
                InstallPath, ExecutablePath, InstalledVersion, TargetVersion,
                CoverUrl, LogoUrl, CustomCoverPath, LastPlayedUtc)
            SELECT
                GameId, BuildId, Symbol, Title, Platform, Architecture, InstallState,
                InstallPath, ExecutablePath, InstalledVersion, TargetVersion,
                CoverUrl, LogoUrl, CustomCoverPath, LastPlayedUtc
            FROM local_installs AS src
            WHERE rowid = (
                SELECT rowid FROM local_installs AS pick
                WHERE pick.GameId = src.GameId
                ORDER BY CASE WHEN pick.InstallState = 4 THEN 0 ELSE 1 END, pick.rowid
                LIMIT 1
            );
            DROP TABLE local_installs;
            ALTER TABLE local_installs_new RENAME TO local_installs;
            CREATE INDEX IF NOT EXISTS IX_local_installs_Symbol ON local_installs (Symbol);
            CREATE INDEX IF NOT EXISTS IX_local_installs_BuildId ON local_installs (BuildId);
            COMMIT;
            """,
            cancellationToken);
    }

    private static async Task EnsureInstallQueueTableAsync(
        SgsClientDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(db, "local_install_queue", cancellationToken))
        {
            await ExecuteNonQueryAsync(
                db,
                """
                CREATE TABLE local_install_queue (
                    GameId INTEGER NOT NULL CONSTRAINT PK_local_install_queue PRIMARY KEY,
                    Version TEXT NULL,
                    IsUpdate INTEGER NOT NULL,
                    Title TEXT NULL,
                    EnqueuedAtUtc TEXT NOT NULL,
                    SortOrder INTEGER NOT NULL DEFAULT 0,
                    IsPaused INTEGER NOT NULL DEFAULT 0
                );
                """,
                cancellationToken);
            return;
        }

        if (!await ColumnExistsAsync(db, "local_install_queue", "SortOrder", cancellationToken))
        {
            await ExecuteNonQueryAsync(
                db,
                "ALTER TABLE local_install_queue ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }

        if (!await ColumnExistsAsync(db, "local_install_queue", "IsPaused", cancellationToken))
        {
            await ExecuteNonQueryAsync(
                db,
                "ALTER TABLE local_install_queue ADD COLUMN IsPaused INTEGER NOT NULL DEFAULT 0;",
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

            if (!await ColumnExistsAsync(db, "local_installs", "BuildId", cancellationToken)
                || !await ColumnExistsAsync(db, "local_installs", "GameId", cancellationToken))
            {
                return false;
            }

            if (!await IsPrimaryKeyAsync(db, "local_installs", "GameId", cancellationToken))
            {
                return false;
            }

            return await TableExistsAsync(db, "local_install_queue", cancellationToken);
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

    private static async Task<bool> IsPrimaryKeyAsync(
        SgsClientDbContext db,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var safeTable = tableName.Replace("'", "''", StringComparison.Ordinal);
        var pkName = await ExecuteScalarStringAsync(
            db,
            $"""
            SELECT name
            FROM pragma_table_info('{safeTable}')
            WHERE pk = 1
            LIMIT 1;
            """,
            cancellationToken);
        return string.Equals(pkName, columnName, StringComparison.OrdinalIgnoreCase);
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

    private static async Task<string?> ExecuteScalarStringAsync(
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
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result);
    }
}
