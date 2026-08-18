using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorStoreFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgeRating",
                table: "games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DiscordUrl",
                table: "games",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Features",
                table: "games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LanguagesCsv",
                table: "games",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleaseDateUtc",
                table: "games",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReleaseStatus",
                table: "games",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<string>(
                name: "StudioName",
                table: "games",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportUrl",
                table: "games",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsCsv",
                table: "games",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "games",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReleaseNotes",
                table: "game_build_versions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "game_downloads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlatformKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DownloadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_downloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_downloads_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_games_ReleaseStatus",
                table: "games",
                column: "ReleaseStatus");

            migrationBuilder.CreateIndex(
                name: "IX_game_downloads_GameId_DownloadedAtUtc",
                table: "game_downloads",
                columns: new[] { "GameId", "DownloadedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_downloads");

            migrationBuilder.DropIndex(
                name: "IX_games_ReleaseStatus",
                table: "games");

            migrationBuilder.DropColumn(
                name: "AgeRating",
                table: "games");

            migrationBuilder.DropColumn(
                name: "DiscordUrl",
                table: "games");

            migrationBuilder.DropColumn(
                name: "Features",
                table: "games");

            migrationBuilder.DropColumn(
                name: "LanguagesCsv",
                table: "games");

            migrationBuilder.DropColumn(
                name: "ReleaseDateUtc",
                table: "games");

            migrationBuilder.DropColumn(
                name: "ReleaseStatus",
                table: "games");

            migrationBuilder.DropColumn(
                name: "StudioName",
                table: "games");

            migrationBuilder.DropColumn(
                name: "SupportUrl",
                table: "games");

            migrationBuilder.DropColumn(
                name: "TagsCsv",
                table: "games");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "games");

            migrationBuilder.DropColumn(
                name: "ReleaseNotes",
                table: "game_build_versions");
        }
    }
}
