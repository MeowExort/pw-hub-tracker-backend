using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalMatchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OriginalMatchId",
                table: "arena_matches",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_arena_matches_OriginalMatchId",
                table: "arena_matches",
                column: "OriginalMatchId",
                filter: "\"OriginalMatchId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_arena_matches_arena_matches_OriginalMatchId",
                table: "arena_matches",
                column: "OriginalMatchId",
                principalTable: "arena_matches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_arena_matches_arena_matches_OriginalMatchId",
                table: "arena_matches");

            migrationBuilder.DropIndex(
                name: "IX_arena_matches_OriginalMatchId",
                table: "arena_matches");

            migrationBuilder.DropColumn(
                name: "OriginalMatchId",
                table: "arena_matches");
        }
    }
}
