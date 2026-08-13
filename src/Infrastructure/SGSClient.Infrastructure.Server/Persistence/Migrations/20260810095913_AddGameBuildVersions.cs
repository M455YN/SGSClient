using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameBuildVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_build_versions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameBuildId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExeName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ZipLink = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_build_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_build_versions_game_builds_GameBuildId",
                        column: x => x.GameBuildId,
                        principalTable: "game_builds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_build_versions_GameBuildId_PublishedAtUtc",
                table: "game_build_versions",
                columns: new[] { "GameBuildId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_game_build_versions_GameBuildId_Version",
                table: "game_build_versions",
                columns: new[] { "GameBuildId", "Version" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO game_build_versions (GameBuildId, Version, ExeName, ZipLink, FileName, PublishedAtUtc)
                SELECT
                    b.Id,
                    b.CurrentVersion,
                    b.ExeName,
                    b.ZipLink,
                    CASE
                        WHEN CHARINDEX('/', REVERSE(b.ZipLink)) > 0
                        THEN RIGHT(b.ZipLink, CHARINDEX('/', REVERSE(b.ZipLink)) - 1)
                        ELSE b.ZipLink
                    END,
                    SYSDATETIMEOFFSET()
                FROM game_builds b
                WHERE b.CurrentVersion IS NOT NULL
                  AND b.ZipLink IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM game_build_versions v
                      WHERE v.GameBuildId = b.Id AND v.Version = b.CurrentVersion);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_build_versions");
        }
    }
}
