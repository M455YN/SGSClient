using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserHideAchievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HideAchievements",
                table: "users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideAchievements",
                table: "users");
        }
    }
}
