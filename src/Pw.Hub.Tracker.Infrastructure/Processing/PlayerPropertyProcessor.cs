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
