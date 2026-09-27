using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations;

/// <inheritdoc />
public partial class SyncPendingModel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Indexes may already exist (FK convention / prior partial apply).
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_user_profile_comments_AuthorUserId'
                  AND object_id = OBJECT_ID(N'user_profile_comments'))
            CREATE INDEX [IX_user_profile_comments_AuthorUserId]
                ON [user_profile_comments] ([AuthorUserId]);
            """);

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_game_reviews_AuthorUserId'
                  AND object_id = OBJECT_ID(N'game_reviews'))
            CREATE INDEX [IX_game_reviews_AuthorUserId]
                ON [game_reviews] ([AuthorUserId]);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_user_profile_comments_AuthorUserId'
                  AND object_id = OBJECT_ID(N'user_profile_comments'))
            DROP INDEX [IX_user_profile_comments_AuthorUserId] ON [user_profile_comments];
            """);

        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE name = N'IX_game_reviews_AuthorUserId'
                  AND object_id = OBJECT_ID(N'game_reviews'))
            DROP INDEX [IX_game_reviews_AuthorUserId] ON [game_reviews];
            """);
    }
}
