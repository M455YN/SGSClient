using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations;

public partial class AddGameLogoUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LogoUrl",
            table: "games",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LogoUrl",
            table: "games");
    }
}
