using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_relationships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequesterUserId = table.Column<int>(type: "int", nullable: false),
                    AddresseeUserId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_relationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_relationships_users_AddresseeUserId",
                        column: x => x.AddresseeUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_relationships_users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_relationships_AddresseeUserId_Kind_Status",
                table: "user_relationships",
                columns: new[] { "AddresseeUserId", "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_relationships_RequesterUserId_AddresseeUserId_Kind",
                table: "user_relationships",
                columns: new[] { "RequesterUserId", "AddresseeUserId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_relationships_RequesterUserId_Kind_Status",
                table: "user_relationships",
                columns: new[] { "RequesterUserId", "Kind", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_relationships");
        }
    }
}
