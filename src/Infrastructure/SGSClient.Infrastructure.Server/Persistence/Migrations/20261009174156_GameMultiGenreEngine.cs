using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GameMultiGenreEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_engine_links",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "int", nullable: false),
                    EngineId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_engine_links", x => new { x.GameId, x.EngineId });
                    table.ForeignKey(
                        name: "FK_game_engine_links_game_engines_EngineId",
                        column: x => x.EngineId,
                        principalTable: "game_engines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_game_engine_links_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_genre_links",
                columns: table => new
                {
                    GameId = table.Column<int>(type: "int", nullable: false),
                    GenreId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_genre_links", x => new { x.GameId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_game_genre_links_game_genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "game_genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_game_genre_links_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_engine_links_EngineId",
                table: "game_engine_links",
                column: "EngineId");

            migrationBuilder.CreateIndex(
                name: "IX_game_genre_links_GenreId",
                table: "game_genre_links",
                column: "GenreId");

            migrationBuilder.Sql(
                "INSERT INTO game_genre_links (GameId, GenreId) SELECT Id, TypeId FROM games WHERE TypeId IS NOT NULL");
            migrationBuilder.Sql(
                "INSERT INTO game_engine_links (GameId, EngineId) SELECT Id, EngineId FROM games WHERE EngineId IS NOT NULL");

            migrationBuilder.DropForeignKey(
                name: "FK_games_game_engines_EngineId",
                table: "games");

            migrationBuilder.DropForeignKey(
                name: "FK_games_game_genres_TypeId",
                table: "games");

            migrationBuilder.DropIndex(
                name: "IX_games_EngineId",
                table: "games");

            migrationBuilder.DropIndex(
                name: "IX_games_TypeId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "EngineId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "games");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EngineId",
                table: "games",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TypeId",
                table: "games",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE g SET g.TypeId = (SELECT MIN(l.GenreId) FROM game_genre_links l WHERE l.GameId = g.Id) FROM games g");
            migrationBuilder.Sql(
                "UPDATE g SET g.EngineId = (SELECT MIN(l.EngineId) FROM game_engine_links l WHERE l.GameId = g.Id) FROM games g");

            migrationBuilder.CreateIndex(
                name: "IX_games_EngineId",
                table: "games",
                column: "EngineId");

            migrationBuilder.CreateIndex(
                name: "IX_games_TypeId",
                table: "games",
                column: "TypeId");

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

            migrationBuilder.DropTable(
                name: "game_engine_links");

            migrationBuilder.DropTable(
                name: "game_genre_links");
        }
    }
}
