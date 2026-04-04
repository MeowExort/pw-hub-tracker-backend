using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingFieldsToPlayerMaxStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AntiInvisibleDegree",
                table: "player_max_stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Armor",
                table: "player_max_stats",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Attack",
                table: "player_max_stats",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "AttackSpeed",
                table: "player_max_stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CritDamageBonus",
                table: "player_max_stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CritRate",
                table: "player_max_stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DamageReduce",
                table: "player_max_stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InvisibleDegree",
                table: "player_max_stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Prayspeed",
                table: "player_max_stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "RunSpeed",
                table: "player_max_stats",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            // Backfill new columns from existing history
            migrationBuilder.Sql("""
                UPDATE player_max_stats pms SET
                    "Attack" = sub."Attack",
                    "Armor" = sub."Armor",
                    "AttackSpeed" = sub."AttackSpeed",
                    "RunSpeed" = sub."RunSpeed",
                    "CritRate" = sub."CritRate",
                    "DamageReduce" = sub."DamageReduce",
                    "Prayspeed" = sub."Prayspeed",
                    "CritDamageBonus" = sub."CritDamageBonus",
                    "InvisibleDegree" = sub."InvisibleDegree",
                    "AntiInvisibleDegree" = sub."AntiInvisibleDegree"
                FROM (
                    SELECT
                        h."PlayerId", h."Server",
                        MAX(h."Attack") AS "Attack",
                        MAX(h."Armor") AS "Armor",
                        MAX(h."AttackSpeed") AS "AttackSpeed",
                        MAX(h."RunSpeed") AS "RunSpeed",
                        MAX(h."CritRate") AS "CritRate",
                        MAX(h."DamageReduce") AS "DamageReduce",
                        MAX(h."Prayspeed") AS "Prayspeed",
                        MAX(h."CritDamageBonus") AS "CritDamageBonus",
                        MAX(h."InvisibleDegree") AS "InvisibleDegree",
                        MAX(h."AntiInvisibleDegree") AS "AntiInvisibleDegree"
                    FROM player_property_history h
                    GROUP BY h."PlayerId", h."Server"
                ) sub
                WHERE pms."PlayerId" = sub."PlayerId" AND pms."Server" = sub."Server"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AntiInvisibleDegree",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "Armor",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "Attack",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "AttackSpeed",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "CritDamageBonus",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "CritRate",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "DamageReduce",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "InvisibleDegree",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "Prayspeed",
                table: "player_max_stats");

            migrationBuilder.DropColumn(
                name: "RunSpeed",
                table: "player_max_stats");
        }
    }
}
