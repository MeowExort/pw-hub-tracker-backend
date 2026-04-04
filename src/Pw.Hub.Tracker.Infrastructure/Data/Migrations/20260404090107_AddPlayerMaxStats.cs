using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerMaxStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_max_stats",
                columns: table => new
                {
                    PlayerId = table.Column<long>(type: "bigint", nullable: false),
                    Server = table.Column<string>(type: "text", nullable: false),
                    Hp = table.Column<long>(type: "bigint", nullable: false),
                    Mp = table.Column<long>(type: "bigint", nullable: false),
                    DamageLow = table.Column<long>(type: "bigint", nullable: false),
                    DamageHigh = table.Column<long>(type: "bigint", nullable: false),
                    DamageMagicLow = table.Column<long>(type: "bigint", nullable: false),
                    DamageMagicHigh = table.Column<long>(type: "bigint", nullable: false),
                    Defense = table.Column<long>(type: "bigint", nullable: false),
                    Resistance = table.Column<long[]>(type: "bigint[]", nullable: false),
                    AttackDegree = table.Column<int>(type: "integer", nullable: false),
                    DefendDegree = table.Column<int>(type: "integer", nullable: false),
                    Vigour = table.Column<long>(type: "bigint", nullable: false),
                    AntiDefenseDegree = table.Column<int>(type: "integer", nullable: false),
                    AntiResistanceDegree = table.Column<int>(type: "integer", nullable: false),
                    PeakGrade = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_max_stats", x => new { x.PlayerId, x.Server });
                });

            // Backfill from existing history
            migrationBuilder.Sql("""
                INSERT INTO player_max_stats ("PlayerId", "Server", "Hp", "Mp", "DamageLow", "DamageHigh",
                    "DamageMagicLow", "DamageMagicHigh", "Defense", "Resistance",
                    "AttackDegree", "DefendDegree", "Vigour",
                    "AntiDefenseDegree", "AntiResistanceDegree", "PeakGrade", "UpdatedAt")
                SELECT
                    h."PlayerId", h."Server",
                    MAX(h."Hp"), MAX(h."Mp"),
                    MAX(h."DamageLow"), MAX(h."DamageHigh"),
                    MAX(h."DamageMagicLow"), MAX(h."DamageMagicHigh"),
                    MAX(h."Defense"),
                    (SELECT h2."Resistance" FROM player_property_history h2
                     WHERE h2."PlayerId" = h."PlayerId" AND h2."Server" = h."Server"
                     ORDER BY h2."RecordedAt" DESC LIMIT 1),
                    MAX(h."AttackDegree"), MAX(h."DefendDegree"),
                    MAX(h."Vigour"),
                    MAX(h."AntiDefenseDegree"), MAX(h."AntiResistanceDegree"),
                    MAX(h."PeakGrade"),
                    MAX(h."RecordedAt")
                FROM player_property_history h
                GROUP BY h."PlayerId", h."Server"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_max_stats");
        }
    }
}
