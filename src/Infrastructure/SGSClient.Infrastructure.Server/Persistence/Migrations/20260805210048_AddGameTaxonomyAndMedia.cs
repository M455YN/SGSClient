using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTaxonomyAndMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverUrl",
                table: "games",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntroUrl",
                table: "games",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "game_engines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_engines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_genres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_screenshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    SourcePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_screenshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_screenshots_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_games_EngineId",
                table: "games",
                column: "EngineId");

            migrationBuilder.CreateIndex(
                name: "IX_games_TypeId",
                table: "games",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_game_engines_Symbol",
                table: "game_engines",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_genres_Symbol",
                table: "game_genres",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_game_screenshots_GameId_SortOrder",
                table: "game_screenshots",
                columns: new[] { "GameId", "SortOrder" });

            // Existing seed games used TypeId/EngineId without lookup tables — clear before FK.
            migrationBuilder.Sql("UPDATE games SET TypeId = NULL, EngineId = NULL;");

            migrationBuilder.AddForeignKey(
                name: "FK_games_game_engines_EngineId",
                table: "games",
                column: "EngineId",
                principalTable: "game_engines",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_games_game_genres_TypeId",
                table: "games",
                column: "TypeId",
                principalTable: "game_genres",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_games_game_engines_EngineId",
                table: "games");

            migrationBuilder.DropForeignKey(
                name: "FK_games_game_genres_TypeId",
                table: "games");

            migrationBuilder.DropTable(
                name: "game_engines");

            migrationBuilder.DropTable(
                name: "game_genres");

            migrationBuilder.DropTable(
                name: "game_screenshots");

            migrationBuilder.DropIndex(
                name: "IX_games_EngineId",
                table: "games");

            migrationBuilder.DropIndex(
                name: "IX_games_TypeId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "CoverUrl",
                table: "games");

            migrationBuilder.DropColumn(
                name: "IntroUrl",
                table: "games");
        }
    }
}
