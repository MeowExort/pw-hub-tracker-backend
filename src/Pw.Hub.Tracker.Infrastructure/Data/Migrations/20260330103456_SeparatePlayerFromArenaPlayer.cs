using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePlayerFromArenaPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "players",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Cls = table.Column<int>(type: "integer", nullable: false),
                    Gender = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_players", x => x.Id);
                });

            // Migrate existing data from arena_players to players
            migrationBuilder.Sql("""
                INSERT INTO players ("Id", "Name", "Cls", "Gender", "UpdatedAt")
                SELECT "Id", "Name", "Cls", 0, "UpdatedAt"
                FROM arena_players
                ON CONFLICT ("Id") DO NOTHING
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_arena_players_players_Id",
                table: "arena_players",
                column: "Id",
                principalTable: "players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "Cls",
                table: "arena_players");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "arena_players");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_arena_players_players_Id",
                table: "arena_players");

            migrationBuilder.DropTable(
                name: "players");

            migrationBuilder.AddColumn<int>(
                name: "Cls",
                table: "arena_players",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "arena_players",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
