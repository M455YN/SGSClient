using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    HardwareRequirements = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    OtherInformation = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    EngineId = table.Column<int>(type: "int", nullable: true),
                    TypeId = table.Column<int>(type: "int", nullable: true),
                    DraftP = table.Column<bool>(type: "bit", nullable: false),
                    FeaturedP = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzureSub = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastLoginUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_builds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    Architecture = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CurrentVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExeName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ZipLink = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_builds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_builds_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_builds_GameId_IsDefault",
                table: "game_builds",
                columns: new[] { "GameId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_game_builds_GameId_Platform_Architecture",
                table: "game_builds",
                columns: new[] { "GameId", "Platform", "Architecture" },
                unique: true,
                filter: "[Architecture] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_games_DraftP",
                table: "games",
                column: "DraftP");

            migrationBuilder.CreateIndex(
                name: "IX_games_FeaturedP",
                table: "games",
                column: "FeaturedP");

            migrationBuilder.CreateIndex(
                name: "IX_games_Symbol",
                table: "games",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_games_UserId",
                table: "games",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_AzureSub",
                table: "users",
                column: "AzureSub",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_builds");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "games");
        }
    }
}
