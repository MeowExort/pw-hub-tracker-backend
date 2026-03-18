using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arena_score_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<short>(type: "smallint", nullable: false),
                    MatchPattern = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    WinCount = table.Column<int>(type: "integer", nullable: false),
                    BattleCount = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arena_score_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "arena_teams",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    CaptainId = table.Column<long>(type: "bigint", nullable: false),
                    ZoneId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    WeekResetTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    LastVisiteTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arena_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "arena_matches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    MatchPattern = table.Column<int>(type: "integer", nullable: false),
                    TeamAId = table.Column<long>(type: "bigint", nullable: true),
                    TeamBId = table.Column<long>(type: "bigint", nullable: true),
                    WinnerTeamId = table.Column<long>(type: "bigint", nullable: true),
                    LoserTeamId = table.Column<long>(type: "bigint", nullable: true),
                    TeamAScoreBefore = table.Column<int>(type: "integer", nullable: true),
                    TeamAScoreAfter = table.Column<int>(type: "integer", nullable: true),
                    TeamBScoreBefore = table.Column<int>(type: "integer", nullable: true),
                    TeamBScoreAfter = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arena_matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_arena_matches_arena_teams_TeamAId",
                        column: x => x.TeamAId,
                        principalTable: "arena_teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_arena_matches_arena_teams_TeamBId",
                        column: x => x.TeamBId,
                        principalTable: "arena_teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "arena_players",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    Cls = table.Column<int>(type: "integer", nullable: false),
                    RewardMoney = table.Column<long>(type: "bigint", nullable: false),
                    WeekResetTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    LastBattleTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    LastVisiteTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arena_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_arena_players_arena_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "arena_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "arena_battle_stats",
                columns: table => new
                {
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<short>(type: "smallint", nullable: false),
                    MatchPattern = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    WinCount = table.Column<int>(type: "integer", nullable: false),
                    BattleCount = table.Column<int>(type: "integer", nullable: false),
                    WeekBattleCount = table.Column<int>(type: "integer", nullable: false),
                    WeekWinCount = table.Column<int>(type: "integer", nullable: false),
                    WeekMaxScore = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    ArenaPlayerId = table.Column<long>(type: "bigint", nullable: true),
                    ArenaTeamId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arena_battle_stats", x => new { x.EntityId, x.EntityType, x.MatchPattern });
                    table.ForeignKey(
                        name: "FK_arena_battle_stats_arena_players_ArenaPlayerId",
                        column: x => x.ArenaPlayerId,
                        principalTable: "arena_players",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_arena_battle_stats_arena_teams_ArenaTeamId",
                        column: x => x.ArenaTeamId,
                        principalTable: "arena_teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "arena_match_participants",
                columns: table => new
                {
                    MatchId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerCls = table.Column<int>(type: "integer", nullable: false),
                    ScoreBefore = table.Column<int>(type: "integer", nullable: true),
                    ScoreAfter = table.Column<int>(type: "integer", nullable: true),
                    IsWinner = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arena_match_participants", x => new { x.MatchId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_arena_match_participants_arena_matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "arena_matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arena_match_participants_arena_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "arena_players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arena_match_participants_arena_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "arena_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "arena_team_members",
                columns: table => new
                {
                    TeamId = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    RewardMoneyInfo = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arena_team_members", x => new { x.TeamId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_arena_team_members_arena_players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "arena_players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_arena_team_members_arena_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "arena_teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_arena_battle_stats_ArenaPlayerId",
                table: "arena_battle_stats",
                column: "ArenaPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_battle_stats_ArenaTeamId",
                table: "arena_battle_stats",
                column: "ArenaTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_match_participants_MatchId_TeamId",
                table: "arena_match_participants",
                columns: new[] { "MatchId", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_match_participants_PlayerId",
                table: "arena_match_participants",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_match_participants_TeamId",
                table: "arena_match_participants",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_matches_TeamAId",
                table: "arena_matches",
                column: "TeamAId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_matches_TeamBId",
                table: "arena_matches",
                column: "TeamBId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_matches_WinnerTeamId",
                table: "arena_matches",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_players_TeamId",
                table: "arena_players",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_arena_score_history_EntityId_EntityType_MatchPattern_Record~",
                table: "arena_score_history",
                columns: new[] { "EntityId", "EntityType", "MatchPattern", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_arena_team_members_PlayerId",
                table: "arena_team_members",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arena_battle_stats");

            migrationBuilder.DropTable(
                name: "arena_match_participants");

            migrationBuilder.DropTable(
                name: "arena_score_history");

            migrationBuilder.DropTable(
                name: "arena_team_members");

            migrationBuilder.DropTable(
                name: "arena_matches");

            migrationBuilder.DropTable(
                name: "arena_players");

            migrationBuilder.DropTable(
                name: "arena_teams");
        }
    }
}
