using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;
namespace Pw.Hub.Tracker.Api.Controllers;
[ApiController]
[Route("api/analytics/servers")]
public class ServerAnalyticsController(TrackerDbContext db) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var playersByServer = await db.Players
            .GroupBy(p => p.Server)
            .Select(g => new { Server = g.Key, PlayerCount = g.Count() })
            .ToListAsync();
        var teamsByServer = await db.ArenaTeamMembers
            .Select(m => m.PlayerServer)
            .Distinct()
            .GroupJoin(db.ArenaTeams,
                server => 1,
                team => 1,
                (server, teams) => new { server })
            .ToListAsync();
        // Simpler approach: count teams that have members from each server
        var serversFromMembers = await db.ArenaTeamMembers
            .Select(m => m.PlayerServer)
            .Distinct()
            .ToListAsync();
        var matchParticipantsByServer = await db.ArenaMatchParticipants
            .GroupBy(p => p.PlayerServer)
            .Select(g => new { Server = g.Key, MatchParticipations = g.Count() })
            .ToListAsync();
        var playersDict = playersByServer.ToDictionary(x => x.Server, x => x.PlayerCount);
        var matchesDict = matchParticipantsByServer.ToDictionary(x => x.Server, x => x.MatchParticipations);
        var allServers = playersDict.Keys
            .Union(matchesDict.Keys)
            .Union(serversFromMembers)
            .Distinct()
            .OrderBy(s => s)
            .ToList();
        var overview = allServers.Select(s => new
        {
            Server = s,
            Players = playersDict.GetValueOrDefault(s, 0),
            MatchParticipations = matchesDict.GetValueOrDefault(s, 0)
        }).ToList();
        return Ok(overview);
    }
    [HttpGet("average-score")]
    public async Task<IActionResult> GetAverageScore([FromQuery] int? matchPattern)
    {
        var query = db.ArenaBattleStats
            .Where(s => s.EntityType == EntityType.Player);
        if (matchPattern.HasValue)
            query = query.Where(s => s.MatchPattern == matchPattern.Value);
        var rawData = await query
            .GroupBy(s => s.Server)
            .Select(g => new
            {
                Server = g.Key,
                AverageScore = g.Average(s => (double)s.Score),
                PlayerCount = g.Count(),
                MaxScore = g.Max(s => s.Score),
                MinScore = g.Min(s => s.Score)
            })
            .OrderByDescending(x => x.AverageScore)
            .ToListAsync();
        var data = rawData.Select(x => new
        {
            x.Server,
            AverageScore = Math.Round(x.AverageScore, 2),
            x.PlayerCount,
            x.MaxScore,
            x.MinScore
        }).ToList();
        return Ok(data);
    }
    [HttpGet("player-stats-comparison")]
    public async Task<IActionResult> GetPlayerStatsComparison()
    {
        var data = await db.PlayerMaxStats
            .GroupBy(p => p.Server)
            .Select(g => new
            {
                Server = g.Key,
                PlayerCount = g.Count(),
                AvgHp = Math.Round(g.Average(p => (double)p.Hp), 0),
                AvgAttack = Math.Round(g.Average(p => (double)p.Attack), 0),
                AvgDefense = Math.Round(g.Average(p => (double)p.Defense), 0),
                AvgArmor = Math.Round(g.Average(p => (double)p.Armor), 0),
                AvgCritRate = Math.Round(g.Average(p => (double)p.CritRate), 2),
                AvgPeakGrade = Math.Round(g.Average(p => (double)p.PeakGrade), 2),
                MaxPeakGrade = g.Max(p => p.PeakGrade)
            })
            .OrderBy(x => x.Server)
            .ToListAsync();
        return Ok(data);
    }
    [HttpGet("{server}/summary")]
    public async Task<IActionResult> GetServerSummary(string server)
    {
        var playerCount = await db.Players.CountAsync(p => p.Server == server);
        if (playerCount == 0)
            return NotFound($"Server '{server}' not found");
        var arenaPlayerCount = await db.ArenaPlayers.CountAsync(p => p.PlayerServer == server);
        var matchCount = await db.ArenaMatchParticipants
            .Where(p => p.PlayerServer == server)
            .Select(p => p.MatchId)
            .Distinct()
            .CountAsync();
        var avgScore = await db.ArenaBattleStats
            .Where(s => s.Server == server && s.EntityType == EntityType.Player)
            .Select(s => (double?)s.Score)
            .AverageAsync() ?? 0;
        var classDistribution = await db.Players
            .Where(p => p.Server == server)
            .GroupBy(p => p.Cls)
            .Select(g => new { Cls = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var topPlayers = await db.ArenaBattleStats
            .Where(s => s.Server == server && s.EntityType == EntityType.Player)
            .OrderByDescending(s => s.Score)
            .Take(10)
            .Join(db.Players,
                s => new { Id = s.EntityId, s.Server },
                p => new { p.Id, p.Server },
                (s, p) => new
                {
                    p.Id,
                    p.Name,
                    p.Cls,
                    s.Score,
                    s.MatchPattern,
                    s.WinCount,
                    s.BattleCount
                })
            .ToListAsync();
        return Ok(new
        {
            Server = server,
            PlayerCount = playerCount,
            ArenaPlayerCount = arenaPlayerCount,
            MatchCount = matchCount,
            AverageScore = Math.Round(avgScore, 2),
            ClassDistribution = classDistribution,
            TopPlayers = topPlayers
        });
    }
}
