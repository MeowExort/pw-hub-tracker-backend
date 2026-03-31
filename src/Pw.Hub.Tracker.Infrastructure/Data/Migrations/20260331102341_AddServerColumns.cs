using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_arena_battle_stats_arena_players_ArenaPlayerId",
                table: "arena_battle_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_arena_match_participants_arena_players_PlayerId",
                table: "arena_match_participants");

            migrationBuilder.DropForeignKey(
                name: "FK_arena_team_members_arena_players_PlayerId",
                table: "arena_team_members");

            migrationBuilder.DropIndex(
                name: "IX_player_property_history_PlayerId_RecordedAt",
                table: "player_property_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_arena_team_members",
                table: "arena_team_members");

            migrationBuilder.DropIndex(
                name: "IX_arena_team_members_PlayerId",
                table: "arena_team_members");

            migrationBuilder.DropPrimaryKey(
                name: "PK_arena_score_history",
                table: "arena_score_history");

            migrationBuilder.DropIndex(
                name: "IX_arena_score_history_EntityId_EntityType_MatchPattern_Record~",
                table: "arena_score_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_arena_players",
                table: "arena_players");

            migrationBuilder.DropIndex(
                name: "IX_arena_players_Id_PlayerServer",
                table: "arena_players");

            migrationBuilder.DropIndex(
                name: "IX_arena_match_participants_PlayerId",
                table: "arena_match_participants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_arena_battle_stats",
                table: "arena_battle_stats");

            migrationBuilder.DropIndex(
                name: "IX_arena_battle_stats_ArenaPlayerId",
                table: "arena_battle_stats");

            migrationBuilder.AddColumn<string>(
                name: "PlayerServer",
                table: "arena_team_members",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Server",
                table: "arena_score_history",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlayerServer",
                table: "arena_match_participants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Server",
                table: "arena_battle_stats",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ArenaPlayerPlayerServer",
                table: "arena_battle_stats",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_arena_team_members",
                table: "arena_team_members",
                columns: new[] { "TeamId", "PlayerId", "PlayerServer" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_arena_score_history",
                table: "arena_score_history",
                columns: new[] { "Id", "Server" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_arena_players",
                table: "arena_players",
                columns: new[] { "Id", "PlayerServer" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_arena_battle_stats",
                table: "arena_battle_stats",
                columns: new[] { "EntityId", "Server", "EntityType", "MatchPattern" });

            migrationBuilder.CreateIndex(
                name: "IX_player_property_history_PlayerId_Server_RecordedAt",
                table: "player_property_history",
                columns: new[] { "PlayerId", "Server", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_team_members_PlayerId_PlayerServer",
                table: "arena_team_members",
                columns: new[] { "PlayerId", "PlayerServer" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_score_history_EntityId_Server_EntityType_MatchPattern~",
                table: "arena_score_history",
                columns: new[] { "EntityId", "Server", "EntityType", "MatchPattern", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_match_participants_PlayerId_PlayerServer",
                table: "arena_match_participants",
                columns: new[] { "PlayerId", "PlayerServer" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_battle_stats_ArenaPlayerId_ArenaPlayerPlayerServer",
                table: "arena_battle_stats",
                columns: new[] { "ArenaPlayerId", "ArenaPlayerPlayerServer" });

            migrationBuilder.AddForeignKey(
                name: "FK_arena_battle_stats_arena_players_ArenaPlayerId_ArenaPlayerP~",
                table: "arena_battle_stats",
                columns: new[] { "ArenaPlayerId", "ArenaPlayerPlayerServer" },
                principalTable: "arena_players",
                principalColumns: new[] { "Id", "PlayerServer" });

            // Backfill PlayerServer in arena_match_participants from arena_players
            migrationBuilder.Sql("""
                UPDATE arena_match_participants amp
                SET "PlayerServer" = ap."PlayerServer"
                FROM arena_players ap
                WHERE amp."PlayerId" = ap."Id" AND amp."PlayerServer" = '';
                """);

            // Delete orphan rows that have no matching arena_player
            migrationBuilder.Sql("""
                DELETE FROM arena_match_participants
                WHERE "PlayerServer" = ''
                   OR NOT EXISTS (
                       SELECT 1 FROM arena_players ap
                       WHERE ap."Id" = arena_match_participants."PlayerId"
                         AND ap."PlayerServer" = arena_match_participants."PlayerServer"
                   );
                """);

            // Backfill PlayerServer in arena_team_members from arena_players
            migrationBuilder.Sql("""
                UPDATE arena_team_members atm
                SET "PlayerServer" = ap."PlayerServer"
                FROM arena_players ap
                WHERE atm."PlayerId" = ap."Id" AND atm."PlayerServer" = '';
                """);

            // Delete orphan rows in arena_team_members
            migrationBuilder.Sql("""
                DELETE FROM arena_team_members
                WHERE "PlayerServer" = ''
                   OR NOT EXISTS (
                       SELECT 1 FROM arena_players ap
                       WHERE ap."Id" = arena_team_members."PlayerId"
                         AND ap."PlayerServer" = arena_team_members."PlayerServer"
                   );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_arena_match_participants_arena_players_PlayerId_PlayerServer",
                table: "arena_match_participants",
                columns: new[] { "PlayerId", "PlayerServer" },
                principalTable: "arena_players",
                principalColumns: new[] { "Id", "PlayerServer" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_arena_team_members_arena_players_PlayerId_PlayerServer",
                table: "arena_team_members",
                columns: new[] { "PlayerId", "PlayerServer" },
                principalTable: "arena_players",
                principalColumns: new[] { "Id", "PlayerServer" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_arena_battle_stats_arena_players_ArenaPlayerId_ArenaPlayerP~",
                table: "arena_battle_stats");

            migrationBuilder.DropForeignKey(
                name: "FK_arena_match_participants_arena_players_PlayerId_PlayerServer",
                table: "arena_match_participants");

            migrationBuilder.DropForeignKey(
                name: "FK_arena_team_members_arena_players_PlayerId_PlayerServer",
                table: "arena_team_members");

            migrationBuilder.DropIndex(
                name: "IX_player_property_history_PlayerId_Server_RecordedAt",
                table: "player_property_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_arena_team_members",
                table: "arena_team_members");

            migrationBuilder.DropIndex(
                name: "IX_arena_team_members_PlayerId_PlayerServer",
                table: "arena_team_members");

            migrationBuilder.DropPrimaryKey(
                name: "PK_arena_score_history",
                table: "arena_score_history");

            migrationBuilder.DropIndex(
                name: "IX_arena_score_history_EntityId_Server_EntityType_MatchPattern~",
                table: "arena_score_history");

            migrationBuilder.DropPrimaryKey(
                name: "PK_arena_players",
                table: "arena_players");

            migrationBuilder.DropIndex(
                name: "IX_arena_match_participants_PlayerId_PlayerServer",
                table: "arena_match_participants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_arena_battle_stats",
                table: "arena_battle_stats");

            migrationBuilder.DropIndex(
                name: "IX_arena_battle_stats_ArenaPlayerId_ArenaPlayerPlayerServer",
                table: "arena_battle_stats");

            migrationBuilder.DropColumn(
                name: "PlayerServer",
                table: "arena_team_members");

            migrationBuilder.DropColumn(
                name: "Server",
                table: "arena_score_history");

            migrationBuilder.DropColumn(
                name: "PlayerServer",
                table: "arena_match_participants");

            migrationBuilder.DropColumn(
                name: "Server",
                table: "arena_battle_stats");

            migrationBuilder.DropColumn(
                name: "ArenaPlayerPlayerServer",
                table: "arena_battle_stats");

            migrationBuilder.AddPrimaryKey(
                name: "PK_arena_team_members",
                table: "arena_team_members",
                columns: new[] { "TeamId", "PlayerId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_arena_score_history",
                table: "arena_score_history",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_arena_players",
                table: "arena_players",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_arena_battle_stats",
                table: "arena_battle_stats",
                columns: new[] { "EntityId", "EntityType", "MatchPattern" });

            migrationBuilder.CreateIndex(
                name: "IX_player_property_history_PlayerId_RecordedAt",
                table: "player_property_history",
                columns: new[] { "PlayerId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_team_members_PlayerId",
                table: "arena_team_members",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_score_history_EntityId_EntityType_MatchPattern_Record~",
                table: "arena_score_history",
                columns: new[] { "EntityId", "EntityType", "MatchPattern", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_players_Id_PlayerServer",
                table: "arena_players",
                columns: new[] { "Id", "PlayerServer" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_match_participants_PlayerId",
                table: "arena_match_participants",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_battle_stats_ArenaPlayerId",
                table: "arena_battle_stats",
                column: "ArenaPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_arena_battle_stats_arena_players_ArenaPlayerId",
                table: "arena_battle_stats",
                column: "ArenaPlayerId",
                principalTable: "arena_players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_arena_match_participants_arena_players_PlayerId",
                table: "arena_match_participants",
                column: "PlayerId",
                principalTable: "arena_players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_arena_team_members_arena_players_PlayerId",
                table: "arena_team_members",
                column: "PlayerId",
                principalTable: "arena_players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
