using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SGSClient.Infrastructure.Server.Persistence.Migrations;

[DbContext(typeof(SgsServerDbContext))]
[Migration("20260927120000_AddReviewVotesAndComments")]
public partial class AddReviewVotesAndComments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "game_review_votes",
            columns: table => new
            {
                ReviewId = table.Column<long>(type: "bigint", nullable: false),
                UserId = table.Column<int>(type: "int", nullable: false),
                IsHelpful = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_game_review_votes", x => new { x.ReviewId, x.UserId });
                table.ForeignKey(
                    name: "FK_game_review_votes_game_reviews_ReviewId",
                    column: x => x.ReviewId,
                    principalTable: "game_reviews",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_game_review_votes_users_UserId",
                    column: x => x.UserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "game_review_comments",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ReviewId = table.Column<long>(type: "bigint", nullable: false),
                AuthorUserId = table.Column<int>(type: "int", nullable: false),
                Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_game_review_comments", x => x.Id);
                table.ForeignKey(
                    name: "FK_game_review_comments_game_reviews_ReviewId",
                    column: x => x.ReviewId,
                    principalTable: "game_reviews",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_game_review_comments_users_AuthorUserId",
                    column: x => x.AuthorUserId,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_game_review_votes_UserId",
            table: "game_review_votes",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_game_review_comments_ReviewId_CreatedAtUtc",
            table: "game_review_comments",
            columns: new[] { "ReviewId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_game_review_comments_AuthorUserId",
            table: "game_review_comments",
            column: "AuthorUserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "game_review_comments");
        migrationBuilder.DropTable(name: "game_review_votes");
    }
}
