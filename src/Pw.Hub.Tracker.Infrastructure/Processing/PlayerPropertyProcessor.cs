using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pw.Hub.Tracker.Sync.Web.Models;

namespace Pw.Hub.Tracker.Infrastructure.Processing;

public class PlayerPropertyProcessor(
    NpgsqlDataSource dataSource,
    ILogger<PlayerPropertyProcessor> logger)
{
    public async Task ProcessAsync(PlayerPropertyMessage message)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var resistance = message.Resistance.ToArray();

            await UpsertPlayerPropertyAsync(connection, transaction, message, resistance);
            await InsertPlayerPropertyHistoryAsync(connection, transaction, message, resistance);
            await UpsertPlayerMaxStatsAsync(connection, transaction, message, resistance);

            await transaction.CommitAsync();

            logger.LogDebug("Processed player property for player {PlayerId} on server {Server}",
                message.PlayerId, message.Server);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task UpsertPlayerPropertyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PlayerPropertyMessage msg, long[] resistance)
    {
        const string sql = """
            INSERT INTO player_properties ("PlayerId", "Server", "Hp", "Mp", "DamageLow", "DamageHigh",
                "DamageMagicLow", "DamageMagicHigh", "Defense", "Resistance", "Attack", "Armor",
                "AttackSpeed", "RunSpeed", "AttackDegree", "DefendDegree", "CritRate", "DamageReduce",
                "Prayspeed", "CritDamageBonus", "InvisibleDegree", "AntiInvisibleDegree", "Vigour",
                "AntiDefenseDegree", "AntiResistanceDegree", "PeakGrade", "UpdatedAt")
            VALUES (@PlayerId, @Server, @Hp, @Mp, @DamageLow, @DamageHigh,
                @DamageMagicLow, @DamageMagicHigh, @Defense, @Resistance, @Attack, @Armor,
                @AttackSpeed, @RunSpeed, @AttackDegree, @DefendDegree, @CritRate, @DamageReduce,
                @Prayspeed, @CritDamageBonus, @InvisibleDegree, @AntiInvisibleDegree, @Vigour,
                @AntiDefenseDegree, @AntiResistanceDegree, @PeakGrade, @UpdatedAt)
            ON CONFLICT ("PlayerId", "Server") DO UPDATE SET
                "Hp" = EXCLUDED."Hp",
                "Mp" = EXCLUDED."Mp",
                "DamageLow" = EXCLUDED."DamageLow",
                "DamageHigh" = EXCLUDED."DamageHigh",
                "DamageMagicLow" = EXCLUDED."DamageMagicLow",
                "DamageMagicHigh" = EXCLUDED."DamageMagicHigh",
                "Defense" = EXCLUDED."Defense",
                "Resistance" = EXCLUDED."Resistance",
                "Attack" = EXCLUDED."Attack",
                "Armor" = EXCLUDED."Armor",
                "AttackSpeed" = EXCLUDED."AttackSpeed",
                "RunSpeed" = EXCLUDED."RunSpeed",
                "AttackDegree" = EXCLUDED."AttackDegree",
                "DefendDegree" = EXCLUDED."DefendDegree",
                "CritRate" = EXCLUDED."CritRate",
                "DamageReduce" = EXCLUDED."DamageReduce",
                "Prayspeed" = EXCLUDED."Prayspeed",
                "CritDamageBonus" = EXCLUDED."CritDamageBonus",
                "InvisibleDegree" = EXCLUDED."InvisibleDegree",
                "AntiInvisibleDegree" = EXCLUDED."AntiInvisibleDegree",
                "Vigour" = EXCLUDED."Vigour",
                "AntiDefenseDegree" = EXCLUDED."AntiDefenseDegree",
                "AntiResistanceDegree" = EXCLUDED."AntiResistanceDegree",
                "PeakGrade" = EXCLUDED."PeakGrade",
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;

        await connection.ExecuteAsync(sql, new
        {
            msg.PlayerId,
            msg.Server,
            msg.Hp,
            msg.Mp,
            msg.DamageLow,
            msg.DamageHigh,
            msg.DamageMagicLow,
            msg.DamageMagicHigh,
            msg.Defense,
            Resistance = resistance,
            msg.Attack,
            msg.Armor,
            msg.AttackSpeed,
            msg.RunSpeed,
            msg.AttackDegree,
            msg.DefendDegree,
            msg.CritRate,
            msg.DamageReduce,
            msg.Prayspeed,
            msg.CritDamageBonus,
            msg.InvisibleDegree,
            msg.AntiInvisibleDegree,
            msg.Vigour,
            msg.AntiDefenseDegree,
            msg.AntiResistanceDegree,
            msg.PeakGrade,
            UpdatedAt = DateTime.UtcNow
        }, transaction);
    }

    private static async Task UpsertPlayerMaxStatsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PlayerPropertyMessage msg, long[] resistance)
    {
        const string sql = """
            INSERT INTO player_max_stats ("PlayerId", "Server", "Hp", "Mp", "DamageLow", "DamageHigh",
                "DamageMagicLow", "DamageMagicHigh", "Defense", "Resistance", "Attack", "Armor",
                "AttackSpeed", "RunSpeed", "AttackDegree", "DefendDegree", "CritRate", "DamageReduce",
                "Prayspeed", "CritDamageBonus", "InvisibleDegree", "AntiInvisibleDegree", "Vigour",
                "AntiDefenseDegree", "AntiResistanceDegree", "PeakGrade", "UpdatedAt")
            VALUES (@PlayerId, @Server, @Hp, @Mp, @DamageLow, @DamageHigh,
                @DamageMagicLow, @DamageMagicHigh, @Defense, @Resistance, @Attack, @Armor,
                @AttackSpeed, @RunSpeed, @AttackDegree, @DefendDegree, @CritRate, @DamageReduce,
                @Prayspeed, @CritDamageBonus, @InvisibleDegree, @AntiInvisibleDegree, @Vigour,
                @AntiDefenseDegree, @AntiResistanceDegree, @PeakGrade, @UpdatedAt)
            ON CONFLICT ("PlayerId", "Server") DO UPDATE SET
                "Hp" = GREATEST(player_max_stats."Hp", EXCLUDED."Hp"),
                "Mp" = GREATEST(player_max_stats."Mp", EXCLUDED."Mp"),
                "DamageLow" = GREATEST(player_max_stats."DamageLow", EXCLUDED."DamageLow"),
                "DamageHigh" = GREATEST(player_max_stats."DamageHigh", EXCLUDED."DamageHigh"),
                "DamageMagicLow" = GREATEST(player_max_stats."DamageMagicLow", EXCLUDED."DamageMagicLow"),
                "DamageMagicHigh" = GREATEST(player_max_stats."DamageMagicHigh", EXCLUDED."DamageMagicHigh"),
                "Defense" = GREATEST(player_max_stats."Defense", EXCLUDED."Defense"),
                "Resistance" = EXCLUDED."Resistance",
                "Attack" = GREATEST(player_max_stats."Attack", EXCLUDED."Attack"),
                "Armor" = GREATEST(player_max_stats."Armor", EXCLUDED."Armor"),
                "AttackSpeed" = GREATEST(player_max_stats."AttackSpeed", EXCLUDED."AttackSpeed"),
                "RunSpeed" = GREATEST(player_max_stats."RunSpeed", EXCLUDED."RunSpeed"),
                "AttackDegree" = GREATEST(player_max_stats."AttackDegree", EXCLUDED."AttackDegree"),
                "DefendDegree" = GREATEST(player_max_stats."DefendDegree", EXCLUDED."DefendDegree"),
                "CritRate" = GREATEST(player_max_stats."CritRate", EXCLUDED."CritRate"),
                "DamageReduce" = GREATEST(player_max_stats."DamageReduce", EXCLUDED."DamageReduce"),
                "Prayspeed" = GREATEST(player_max_stats."Prayspeed", EXCLUDED."Prayspeed"),
                "CritDamageBonus" = GREATEST(player_max_stats."CritDamageBonus", EXCLUDED."CritDamageBonus"),
                "InvisibleDegree" = GREATEST(player_max_stats."InvisibleDegree", EXCLUDED."InvisibleDegree"),
                "AntiInvisibleDegree" = GREATEST(player_max_stats."AntiInvisibleDegree", EXCLUDED."AntiInvisibleDegree"),
                "Vigour" = GREATEST(player_max_stats."Vigour", EXCLUDED."Vigour"),
                "AntiDefenseDegree" = GREATEST(player_max_stats."AntiDefenseDegree", EXCLUDED."AntiDefenseDegree"),
                "AntiResistanceDegree" = GREATEST(player_max_stats."AntiResistanceDegree", EXCLUDED."AntiResistanceDegree"),
                "PeakGrade" = GREATEST(player_max_stats."PeakGrade", EXCLUDED."PeakGrade"),
                "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;

        await connection.ExecuteAsync(sql, new
        {
            msg.PlayerId,
            msg.Server,
            msg.Hp,
            msg.Mp,
            msg.DamageLow,
            msg.DamageHigh,
            msg.DamageMagicLow,
            msg.DamageMagicHigh,
            msg.Defense,
            Resistance = resistance,
            msg.Attack,
            msg.Armor,
            msg.AttackSpeed,
            msg.RunSpeed,
            msg.AttackDegree,
            msg.DefendDegree,
            msg.CritRate,
            msg.DamageReduce,
            msg.Prayspeed,
            msg.CritDamageBonus,
            msg.InvisibleDegree,
            msg.AntiInvisibleDegree,
            msg.Vigour,
            msg.AntiDefenseDegree,
            msg.AntiResistanceDegree,
            msg.PeakGrade,
            UpdatedAt = DateTime.UtcNow
        }, transaction);
    }

    private static async Task InsertPlayerPropertyHistoryAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        PlayerPropertyMessage msg, long[] resistance)
    {
        const string sql = """
            INSERT INTO player_property_history ("PlayerId", "Server", "Hp", "Mp", "DamageLow", "DamageHigh",
                "DamageMagicLow", "DamageMagicHigh", "Defense", "Resistance", "Attack", "Armor",
                "AttackSpeed", "RunSpeed", "AttackDegree", "DefendDegree", "CritRate", "DamageReduce",
                "Prayspeed", "CritDamageBonus", "InvisibleDegree", "AntiInvisibleDegree", "Vigour",
                "AntiDefenseDegree", "AntiResistanceDegree", "PeakGrade", "RecordedAt")
            VALUES (@PlayerId, @Server, @Hp, @Mp, @DamageLow, @DamageHigh,
                @DamageMagicLow, @DamageMagicHigh, @Defense, @Resistance, @Attack, @Armor,
                @AttackSpeed, @RunSpeed, @AttackDegree, @DefendDegree, @CritRate, @DamageReduce,
                @Prayspeed, @CritDamageBonus, @InvisibleDegree, @AntiInvisibleDegree, @Vigour,
                @AntiDefenseDegree, @AntiResistanceDegree, @PeakGrade, @RecordedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            msg.PlayerId,
            msg.Server,
            msg.Hp,
            msg.Mp,
            msg.DamageLow,
            msg.DamageHigh,
            msg.DamageMagicLow,
            msg.DamageMagicHigh,
            msg.Defense,
            Resistance = resistance,
            msg.Attack,
            msg.Armor,
            msg.AttackSpeed,
            msg.RunSpeed,
            msg.AttackDegree,
            msg.DefendDegree,
            msg.CritRate,
            msg.DamageReduce,
            msg.Prayspeed,
            msg.CritDamageBonus,
            msg.InvisibleDegree,
            msg.AntiInvisibleDegree,
            msg.Vigour,
            msg.AntiDefenseDegree,
            msg.AntiResistanceDegree,
            msg.PeakGrade,
            RecordedAt = DateTime.UtcNow
        }, transaction);
    }
}
