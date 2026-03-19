using System.Text.Json;
using StackExchange.Redis;

namespace Pw.Hub.Tracker.Infrastructure.Cache;

public record BattleSnapshot(int Score, int BattleCount, int WinCount);

public class ArenaStateCache(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    private static string TeamKey(long teamId, int matchPattern) =>
        $"arena:team:{teamId}:mp:{matchPattern}";

    private static string PlayerKey(long playerId, int matchPattern) =>
        $"arena:player:{playerId}:mp:{matchPattern}";

    private static string TeamLastBattleKey(long teamId) =>
        $"arena:team:{teamId}:last_battle_ts";

    public async Task<BattleSnapshot?> GetTeamSnapshotAsync(long teamId, int matchPattern)
    {
        var val = await _db.StringGetAsync(TeamKey(teamId, matchPattern));
        return val.IsNullOrEmpty ? null : JsonSerializer.Deserialize<BattleSnapshot>((string)val!);
    }

    public async Task SetTeamSnapshotAsync(long teamId, int matchPattern, BattleSnapshot snapshot)
    {
        await _db.StringSetAsync(TeamKey(teamId, matchPattern),
            JsonSerializer.Serialize(snapshot), TimeSpan.FromDays(30));
    }

    public async Task<BattleSnapshot?> GetPlayerSnapshotAsync(long playerId, int matchPattern)
    {
        var val = await _db.StringGetAsync(PlayerKey(playerId, matchPattern));
        return val.IsNullOrEmpty ? null : JsonSerializer.Deserialize<BattleSnapshot>((string)val!);
    }

    public async Task SetPlayerSnapshotAsync(long playerId, int matchPattern, BattleSnapshot snapshot)
    {
        await _db.StringSetAsync(PlayerKey(playerId, matchPattern),
            JsonSerializer.Serialize(snapshot), TimeSpan.FromDays(30));
    }

    public async Task<long?> GetTeamLastBattleTimestampAsync(long teamId)
    {
        var val = await _db.StringGetAsync(TeamLastBattleKey(teamId));
        return val.IsNullOrEmpty ? null : (long?)long.Parse((string)val!);
    }

    public async Task SetTeamLastBattleTimestampAsync(long teamId, long timestamp)
    {
        await _db.StringSetAsync(TeamLastBattleKey(teamId),
            timestamp.ToString(), TimeSpan.FromDays(30));
    }
}
