using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pw.Hub.Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "player_properties",
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
                    Attack = table.Column<long>(type: "bigint", nullable: false),
                    Armor = table.Column<long>(type: "bigint", nullable: false),
                    AttackSpeed = table.Column<int>(type: "integer", nullable: false),
                    RunSpeed = table.Column<double>(type: "double precision", nullable: false),
                    AttackDegree = table.Column<int>(type: "integer", nullable: false),
                    DefendDegree = table.Column<int>(type: "integer", nullable: false),
                    CritRate = table.Column<int>(type: "integer", nullable: false),
                    DamageReduce = table.Column<int>(type: "integer", nullable: false),
                    Prayspeed = table.Column<int>(type: "integer", nullable: false),
                    CritDamageBonus = table.Column<int>(type: "integer", nullable: false),
                    InvisibleDegree = table.Column<int>(type: "integer", nullable: false),
                    AntiInvisibleDegree = table.Column<int>(type: "integer", nullable: false),
                    Vigour = table.Column<long>(type: "bigint", nullable: false),
                    AntiDefenseDegree = table.Column<int>(type: "integer", nullable: false),
                    AntiResistanceDegree = table.Column<int>(type: "integer", nullable: false),
                    PeakGrade = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_properties", x => x.PlayerId);
                });

            migrationBuilder.CreateTable(
                name: "player_property_history",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
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
                    Attack = table.Column<long>(type: "bigint", nullable: false),
                    Armor = table.Column<long>(type: "bigint", nullable: false),
                    AttackSpeed = table.Column<int>(type: "integer", nullable: false),
                    RunSpeed = table.Column<double>(type: "double precision", nullable: false),
                    AttackDegree = table.Column<int>(type: "integer", nullable: false),
                    DefendDegree = table.Column<int>(type: "integer", nullable: false),
                    CritRate = table.Column<int>(type: "integer", nullable: false),
                    DamageReduce = table.Column<int>(type: "integer", nullable: false),
                    Prayspeed = table.Column<int>(type: "integer", nullable: false),
                    CritDamageBonus = table.Column<int>(type: "integer", nullable: false),
                    InvisibleDegree = table.Column<int>(type: "integer", nullable: false),
                    AntiInvisibleDegree = table.Column<int>(type: "integer", nullable: false),
                    Vigour = table.Column<long>(type: "bigint", nullable: false),
                    AntiDefenseDegree = table.Column<int>(type: "integer", nullable: false),
                    AntiResistanceDegree = table.Column<int>(type: "integer", nullable: false),
                    PeakGrade = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_property_history", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_player_property_history_PlayerId_RecordedAt",
                table: "player_property_history",
                columns: new[] { "PlayerId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_properties");

            migrationBuilder.DropTable(
                name: "player_property_history");
        }
    }
}
