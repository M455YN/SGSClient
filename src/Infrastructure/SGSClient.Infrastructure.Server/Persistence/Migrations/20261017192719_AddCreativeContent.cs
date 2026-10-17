using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreativeContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Old Book(3)/Music(4)/Art(5) test content lived in games; all game dependents cascade except DLC parent links.
            migrationBuilder.Sql("DELETE FROM games WHERE ParentGameId IN (SELECT Id FROM games WHERE Kind IN (3, 4, 5))");
            migrationBuilder.Sql("DELETE FROM games WHERE Kind IN (3, 4, 5)");

            migrationBuilder.CreateTable(
                name: "artworks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TagsCsv = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StoredFileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artworks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TagsCsv = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CoverFileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TagsCsv = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    StoredFileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    CoverFileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "story_chapters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    Index = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_story_chapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_story_chapters_stories_StoryId",
                        column: x => x.StoryId,
                        principalTable: "stories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artworks_CreatedAtUtc",
                table: "artworks",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_artworks_UserId",
                table: "artworks",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_stories_CreatedAtUtc",
                table: "stories",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_stories_UserId",
                table: "stories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_story_chapters_StoryId_Index",
                table: "story_chapters",
                columns: new[] { "StoryId", "Index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tracks_CreatedAtUtc",
                table: "tracks",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_tracks_UserId",
                table: "tracks",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artworks");

            migrationBuilder.DropTable(
                name: "story_chapters");

            migrationBuilder.DropTable(
                name: "tracks");

            migrationBuilder.DropTable(
                name: "stories");
        }
    }
}
