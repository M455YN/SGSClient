using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations;

[DbContext(typeof(SgsServerDbContext))]
[Migration("20260905190000_AddReviewsProfileCommentsChat")]
public partial class AddReviewsProfileCommentsChat : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "game_reviews",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                GameId = table.Column<int>(type: "int", nullable: false),
                AuthorUserId = table.Column<int>(type: "int", nullable: false),
                Recommended = table.Column<bool>(type: "bit", nullable: false),
                Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                PlaytimeSecondsAtReview = table.Column<long>(type: "bigint", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_game_reviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_game_reviews_games_GameId",
                    column: x => x.GameId,
                    principalTable: "games",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_game_reviews_users_AuthorUserId",
                    column: x => x.AuthorUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_profile_comments",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                TargetUserId = table.Column<int>(type: "int", nullable: false),
                AuthorUserId = table.Column<int>(type: "int", nullable: false),
                Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_profile_comments", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_profile_comments_users_AuthorUserId",
                    column: x => x.AuthorUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_profile_comments_users_TargetUserId",
                    column: x => x.TargetUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "direct_messages",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SenderUserId = table.Column<int>(type: "int", nullable: false),
                RecipientUserId = table.Column<int>(type: "int", nullable: false),
                Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_direct_messages", x => x.Id);
                table.ForeignKey(
                    name: "FK_direct_messages_users_RecipientUserId",
                    column: x => x.RecipientUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_direct_messages_users_SenderUserId",
                    column: x => x.SenderUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_game_reviews_AuthorUserId",
            table: "game_reviews",
            column: "AuthorUserId");

        migrationBuilder.CreateIndex(
            name: "IX_game_reviews_GameId_AuthorUserId",
            table: "game_reviews",
            columns: new[] { "GameId", "AuthorUserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_game_reviews_GameId_UpdatedAtUtc",
            table: "game_reviews",
            columns: new[] { "GameId", "UpdatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_user_profile_comments_AuthorUserId",
            table: "user_profile_comments",
            column: "AuthorUserId");

        migrationBuilder.CreateIndex(
            name: "IX_user_profile_comments_TargetUserId_CreatedAtUtc",
            table: "user_profile_comments",
            columns: new[] { "TargetUserId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_direct_messages_RecipientUserId_ReadAtUtc",
            table: "direct_messages",
            columns: new[] { "RecipientUserId", "ReadAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_direct_messages_SenderUserId_RecipientUserId_CreatedAtUtc",
            table: "direct_messages",
            columns: new[] { "SenderUserId", "RecipientUserId", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "direct_messages");
        migrationBuilder.DropTable(name: "user_profile_comments");
        migrationBuilder.DropTable(name: "game_reviews");
    }
}
