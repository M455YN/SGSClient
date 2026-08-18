using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGamePlaytime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_game_playtime",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    TotalSeconds = table.Column<long>(type: "bigint", nullable: false),
                    LastPlayedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SessionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_game_playtime", x => new { x.UserId, x.GameId });
                    table.ForeignKey(
                        name: "FK_user_game_playtime_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_game_playtime_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_game_playtime_GameId",
                table: "user_game_playtime",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_user_game_playtime_UserId_LastPlayedUtc",
                table: "user_game_playtime",
                columns: new[] { "UserId", "LastPlayedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_game_playtime");
        }
    }
}
