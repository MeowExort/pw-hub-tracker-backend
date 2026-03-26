using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pw.Hub.Tracker.Sync.Web.Models;

namespace Pw.Hub.Tracker.Infrastructure.Processing;

public class PlayerBaseBriefProcessor(
    NpgsqlDataSource dataSource,
    ILogger<PlayerBaseBriefProcessor> logger)
{
    public async Task ProcessAsync(PlayerBaseBriefMessage message)
    {
        await using var connection = await dataSource.OpenConnectionAsync();

        const string sql = """
            UPDATE arena_players
            SET "Name" = @Name, "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @RoleId
            """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            message.RoleId,
            message.Name,
            UpdatedAt = DateTime.UtcNow
        });

        logger.LogDebug("Updated player base brief for role {RoleId} on server {Server}, affected {Affected} rows",
            message.RoleId, message.Server, affected);
    }
}
