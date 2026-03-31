using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPlayerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_arena_players_players_Id",
                table: "arena_players");

            migrationBuilder.DropPrimaryKey(
                name: "PK_players",
                table: "players");

            migrationBuilder.DropPrimaryKey(
                name: "PK_player_properties",
                table: "player_properties");

            migrationBuilder.AddColumn<string>(
                name: "Server",
                table: "players",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlayerServer",
                table: "arena_players",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_players",
                table: "players",
                columns: new[] { "Id", "Server" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_player_properties",
                table: "player_properties",
                columns: new[] { "PlayerId", "Server" });

            migrationBuilder.CreateIndex(
                name: "IX_player_property_history_PlayerId_Server",
                table: "player_property_history",
                columns: new[] { "PlayerId", "Server" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_players_Id_PlayerServer",
                table: "arena_players",
                columns: new[] { "Id", "PlayerServer" });

            migrationBuilder.AddForeignKey(
                name: "FK_arena_players_players_Id_PlayerServer",
                table: "arena_players",
                columns: new[] { "Id", "PlayerServer" },
                principalTable: "players",
                principalColumns: new[] { "Id", "Server" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_arena_players_players_Id_PlayerServer",
                table: "arena_players");

            migrationBuilder.DropPrimaryKey(
                name: "PK_players",
                table: "players");

            migrationBuilder.DropIndex(
                name: "IX_player_property_history_PlayerId_Server",
                table: "player_property_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_player_properties",
                table: "player_properties");

            migrationBuilder.DropIndex(
                name: "IX_arena_players_Id_PlayerServer",
                table: "arena_players");

            migrationBuilder.DropColumn(
                name: "Server",
                table: "players");

            migrationBuilder.DropColumn(
                name: "PlayerServer",
                table: "arena_players");

            migrationBuilder.AddPrimaryKey(
                name: "PK_players",
                table: "players",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_player_properties",
                table: "player_properties",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_arena_players_players_Id",
                table: "arena_players",
                column: "Id",
                principalTable: "players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
