using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAchievementStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LockedIconUrl",
                table: "game_achievements",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StatKey",
                table: "game_achievements",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "game_stats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    MinValue = table.Column<double>(type: "float", nullable: true),
                    MaxValue = table.Column<double>(type: "float", nullable: true),
                    DefaultValue = table.Column<double>(type: "float", nullable: false),
                    MaxChange = table.Column<double>(type: "float", nullable: true),
                    IncrementOnly = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_stats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_stats_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_game_stats",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    StatId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<double>(type: "float", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_game_stats", x => new { x.UserId, x.StatId });
                    table.ForeignKey(
                        name: "FK_user_game_stats_game_stats_StatId",
                        column: x => x.StatId,
                        principalTable: "game_stats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_game_stats_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_stats_GameId_Key",
                table: "game_stats",
                columns: new[] { "GameId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_game_stats_StatId",
                table: "user_game_stats",
                column: "StatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_game_stats");

            migrationBuilder.DropTable(
                name: "game_stats");

            migrationBuilder.DropColumn(
                name: "LockedIconUrl",
                table: "game_achievements");

            migrationBuilder.DropColumn(
                name: "StatKey",
                table: "game_achievements");
        }
    }
}
