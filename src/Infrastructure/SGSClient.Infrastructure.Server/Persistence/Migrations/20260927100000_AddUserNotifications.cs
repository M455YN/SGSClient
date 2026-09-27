using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations;

[DbContext(typeof(SgsServerDbContext))]
[Migration("20260927100000_AddUserNotifications")]
public partial class AddUserNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_notifications",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<int>(type: "int", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                DataJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_notifications", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_notifications_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_user_notifications_UserId_CreatedAtUtc",
            table: "user_notifications",
            columns: new[] { "UserId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_user_notifications_UserId_ReadAtUtc",
            table: "user_notifications",
            columns: new[] { "UserId", "ReadAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_notifications");
    }
}
