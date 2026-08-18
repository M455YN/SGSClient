using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeaturedOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FeaturedOrder",
                table: "games",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_games_FeaturedOrder",
                table: "games",
                column: "FeaturedOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_games_FeaturedOrder",
                table: "games");

            migrationBuilder.DropColumn(
                name: "FeaturedOrder",
                table: "games");
        }
    }
}
