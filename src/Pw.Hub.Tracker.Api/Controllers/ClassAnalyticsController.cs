using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;
namespace Pw.Hub.Tracker.Api.Controllers;
[ApiController]
[Route("api/analytics/classes")]
public class ClassAnalyticsController(TrackerDbContext db) : ControllerBase
{
    [HttpGet("distribution")]
    public async Task<IActionResult> GetDistribution([FromQuery] int? matchPattern)
    {
        var query = db.ArenaMatchParticipants.AsQueryable();
        if (matchPattern.HasValue)
            query = query.Where(p => p.Match.MatchPattern == matchPattern.Value);
        var raw = await query
            .GroupBy(p => p.PlayerCls)
            .Select(g => new
            {
                Cls = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var uniqueByClass = (await query
            .Select(p => new { p.PlayerCls, p.PlayerId, p.PlayerServer })
            .Distinct()
            .ToListAsync())
            .GroupBy(p => p.PlayerCls)
            .ToDictionary(g => g.Key, g => g.Count());
        var distribution = raw.Select(r => new
        {
            r.Cls,
            r.Count,
            UniquePlayers = uniqueByClass.GetValueOrDefault(r.Cls, 0)
        }).ToList();
        return Ok(distribution);
    }
    [HttpGet("winrate")]
    public async Task<IActionResult> GetWinrate([FromQuery] int? matchPattern)
    {
        var query = db.ArenaMatchParticipants.AsQueryable();
        if (matchPattern.HasValue)
            query = query.Where(p => p.Match.MatchPattern == matchPattern.Value);
        var rawWinrate = await query
            .GroupBy(p => p.PlayerCls)
            .Select(g => new
            {
                Cls = g.Key,
                TotalMatches = g.Count(),
                Wins = g.Count(p => p.IsWinner)
            })
            .ToListAsync();
        var winrate = rawWinrate
            .Select(g => new
            {
                g.Cls,
                g.TotalMatches,
                g.Wins,
                WinRate = g.TotalMatches > 0
                    ? Math.Round((double)g.Wins / g.TotalMatches * 100, 2)
                    : 0
            })
            .OrderByDescending(x => x.WinRate)
            .ToList();
        return Ok(winrate);
    }
    [HttpGet("average-score")]
    public async Task<IActionResult> GetAverageScore([FromQuery] int? matchPattern)
    {
        var query = db.ArenaBattleStats
            .Where(s => s.EntityType == EntityType.Player);
        if (matchPattern.HasValue)
            query = query.Where(s => s.MatchPattern == matchPattern.Value);
        var rawAvgScore = await query
            .Join(db.Players,
                s => new { Id = s.EntityId, s.Server },
                p => new { p.Id, p.Server },
                (s, p) => new { p.Cls, s.Score })
            .GroupBy(x => x.Cls)
            .Select(g => new
            {
                Cls = g.Key,
                AverageScore = g.Average(x => (double)x.Score),
                PlayerCount = g.Count()
            })
            .OrderByDescending(x => x.AverageScore)
            .ToListAsync();
        var avgScore = rawAvgScore.Select(x => new
        {
            x.Cls,
            AverageScore = Math.Round(x.AverageScore, 2),
            x.PlayerCount
        }).ToList();
        return Ok(avgScore);
    }
    [HttpGet("popular-compositions")]
    public async Task<IActionResult> GetPopularCompositions(
        [FromQuery] int? matchPattern,
        [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = db.ArenaMatchParticipants.AsQueryable();
        if (matchPattern.HasValue)
            query = query.Where(p => p.Match.MatchPattern == matchPattern.Value);
        var teamComps = await query
            .GroupBy(p => new { p.MatchId, p.TeamId })
            .Select(g => new
            {
                g.Key.MatchId,
                g.Key.TeamId,
                Classes = g.OrderBy(p => p.PlayerCls).Select(p => p.PlayerCls).ToList(),
                IsWinner = g.Any(p => p.IsWinner)
            })
            .ToListAsync();
        var compositions = teamComps
            .GroupBy(t => string.Join(",", t.Classes))
            .Select(g => new
            {
                Composition = g.First().Classes,
                Count = g.Count(),
                Wins = g.Count(t => t.IsWinner),
                WinRate = g.Count() > 0
                    ? Math.Round((double)g.Count(t => t.IsWinner) / g.Count() * 100, 2)
                    : 0
            })
            .OrderByDescending(x => x.Count)
            .Take(limit)
            .ToList();
        return Ok(compositions);
    }
    [HttpGet("best-compositions")]
    public async Task<IActionResult> GetBestCompositions(
        [FromQuery] int? matchPattern,
        [FromQuery] int minMatches = 5,
        [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = db.ArenaMatchParticipants.AsQueryable();
        if (matchPattern.HasValue)
            query = query.Where(p => p.Match.MatchPattern == matchPattern.Value);
        var teamComps = await query
            .GroupBy(p => new { p.MatchId, p.TeamId })
            .Select(g => new
            {
                g.Key.MatchId,
                g.Key.TeamId,
                Classes = g.OrderBy(p => p.PlayerCls).Select(p => p.PlayerCls).ToList(),
                IsWinner = g.Any(p => p.IsWinner)
            })
            .ToListAsync();
        var compositions = teamComps
            .GroupBy(t => string.Join(",", t.Classes))
            .Where(g => g.Count() >= minMatches)
            .Select(g => new
            {
                Composition = g.First().Classes,
                Count = g.Count(),
                Wins = g.Count(t => t.IsWinner),
                WinRate = g.Count() > 0
                    ? Math.Round((double)g.Count(t => t.IsWinner) / g.Count() * 100, 2)
                    : 0
            })
            .OrderByDescending(x => x.WinRate)
            .Take(limit)
            .ToList();
        return Ok(compositions);
    }
}
