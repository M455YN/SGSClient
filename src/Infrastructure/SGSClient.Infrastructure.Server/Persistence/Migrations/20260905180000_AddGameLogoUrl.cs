using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations;

[DbContext(typeof(SgsServerDbContext))]
[Migration("20260905180000_AddGameLogoUrl")]
public partial class AddGameLogoUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: older DBs may already have the column or missed this migration
        // when it lacked [Migration] metadata.
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'games', N'LogoUrl') IS NULL
            ALTER TABLE [games] ADD [LogoUrl] nvarchar(2048) NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH(N'games', N'LogoUrl') IS NOT NULL
            ALTER TABLE [games] DROP COLUMN [LogoUrl];
            """);
    }
}
