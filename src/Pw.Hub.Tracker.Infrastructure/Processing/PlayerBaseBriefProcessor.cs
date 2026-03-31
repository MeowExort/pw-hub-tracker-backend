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
            INSERT INTO players ("Id", "Name", "Cls", "Gender", "Server", "UpdatedAt")
            VALUES (@RoleId, @Name, @Cls, @Gender, @Server, @UpdatedAt)
            ON CONFLICT ("Id", "Server") DO UPDATE
            SET "Name" = @Name, "Cls" = @Cls, "Gender" = @Gender, "UpdatedAt" = @UpdatedAt
            """;

        var affected = await connection.ExecuteAsync(sql, new
        {
            message.RoleId,
            message.Name,
            message.Cls,
            message.Gender,
            message.Server,
            UpdatedAt = DateTime.UtcNow
        });

        logger.LogDebug("Updated player base brief for role {RoleId} on server {Server}, affected {Affected} rows",
            message.RoleId, message.Server, affected);
    }
}
