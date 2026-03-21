using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberCountToScoreHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MemberCount",
                table: "arena_score_history",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MemberCount",
                table: "arena_score_history");
        }
    }
}
