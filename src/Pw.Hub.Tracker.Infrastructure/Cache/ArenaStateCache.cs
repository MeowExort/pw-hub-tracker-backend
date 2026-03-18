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

    private static string TeamMatchIdsKey(long teamId) =>
        $"arena:team:{teamId}:match_ids";

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

    public async Task<HashSet<long>> GetTeamMatchIdsAsync(long teamId)
    {
        var val = await _db.StringGetAsync(TeamMatchIdsKey(teamId));
        if (val.IsNullOrEmpty) return [];
        return JsonSerializer.Deserialize<HashSet<long>>((string)val!) ?? [];
    }

    public async Task SetTeamMatchIdsAsync(long teamId, HashSet<long> matchIds)
    {
        await _db.StringSetAsync(TeamMatchIdsKey(teamId),
            JsonSerializer.Serialize(matchIds), TimeSpan.FromDays(30));
    }
}
