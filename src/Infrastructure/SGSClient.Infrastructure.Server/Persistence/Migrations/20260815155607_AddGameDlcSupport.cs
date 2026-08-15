using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameDlcSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "games",
                type: "datetimeoffset",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "games",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentGameId",
                table: "games",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PackageSizeBytes",
                table: "game_builds",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_games_Kind",
                table: "games",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_games_ParentGameId",
                table: "games",
                column: "ParentGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_games_games_ParentGameId",
                table: "games",
                column: "ParentGameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_games_games_ParentGameId",
                table: "games");

            migrationBuilder.DropIndex(
                name: "IX_games_Kind",
                table: "games");

            migrationBuilder.DropIndex(
                name: "IX_games_ParentGameId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "games");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "games");

            migrationBuilder.DropColumn(
                name: "ParentGameId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "PackageSizeBytes",
                table: "game_builds");
        }
    }
}
