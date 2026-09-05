using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    [DbContext(typeof(SgsServerDbContext))]
    [Migration("20260905160000_AddEntitlementsWishlistPresence")]
    public partial class AddEntitlementsWishlistPresence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenUtc",
                table: "users",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_game_entitlements",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    GrantedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_game_entitlements", x => new { x.UserId, x.GameId });
                    table.ForeignKey(
                        name: "FK_user_game_entitlements_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_game_entitlements_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_game_wishlists",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    GameId = table.Column<int>(type: "int", nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_game_wishlists", x => new { x.UserId, x.GameId });
                    table.ForeignKey(
                        name: "FK_user_game_wishlists_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_game_wishlists_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_LastSeenUtc",
                table: "users",
                column: "LastSeenUtc");

            migrationBuilder.CreateIndex(
                name: "IX_user_game_entitlements_GameId",
                table: "user_game_entitlements",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_user_game_entitlements_UserId_GrantedAtUtc",
                table: "user_game_entitlements",
                columns: new[] { "UserId", "GrantedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_user_game_wishlists_GameId",
                table: "user_game_wishlists",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_user_game_wishlists_UserId_AddedAtUtc",
                table: "user_game_wishlists",
                columns: new[] { "UserId", "AddedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "user_game_entitlements");
            migrationBuilder.DropTable(name: "user_game_wishlists");
            migrationBuilder.DropIndex(name: "IX_users_LastSeenUtc", table: "users");
            migrationBuilder.DropColumn(name: "LastSeenUtc", table: "users");
        }
    }
}
