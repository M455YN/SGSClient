using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameHeaderUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeaderUrl",
                table: "games",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeaderUrl",
                table: "games");
        }
    }
}
