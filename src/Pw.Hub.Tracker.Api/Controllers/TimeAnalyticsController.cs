using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Infrastructure.Data;
namespace Pw.Hub.Tracker.Api.Controllers;
[ApiController]
[Route("api/analytics/time")]
public class TimeAnalyticsController(TrackerDbContext db) : ControllerBase
{
    [HttpGet("matches-per-day")]
    public async Task<IActionResult> GetMatchesPerDay(
        [FromQuery] int? matchPattern,
        [FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-days);
        var query = db.ArenaMatches.Where(m => m.CreatedAt >= since);
        if (matchPattern.HasValue)
            query = query.Where(m => m.MatchPattern == matchPattern.Value);
        var data = await query
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync();
        return Ok(data);
    }
    [HttpGet("matches-per-hour")]
    public async Task<IActionResult> GetMatchesPerHour(
        [FromQuery] int? matchPattern,
        [FromQuery] int days = 7)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTime.UtcNow.AddDays(-days);
        var query = db.ArenaMatches.Where(m => m.CreatedAt >= since);
        if (matchPattern.HasValue)
            query = query.Where(m => m.MatchPattern == matchPattern.Value);
        var data = await query
            .GroupBy(m => m.CreatedAt.Hour)
            .Select(g => new
            {
                Hour = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.Hour)
            .ToListAsync();
        return Ok(data);
    }
    [HttpGet("matches-by-day-of-week")]
    public async Task<IActionResult> GetMatchesByDayOfWeek(
        [FromQuery] int? matchPattern,
        [FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-days);
        var query = db.ArenaMatches.Where(m => m.CreatedAt >= since);
        if (matchPattern.HasValue)
            query = query.Where(m => m.MatchPattern == matchPattern.Value);
        var data = await query
            .GroupBy(m => m.CreatedAt.DayOfWeek)
            .Select(g => new
            {
                DayOfWeek = g.Key,
                Count = g.Count()
            })
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync();
        return Ok(data);
    }
    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] int? matchPattern,
        [FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-days);
        var query = db.ArenaMatches.Where(m => m.CreatedAt >= since);
        if (matchPattern.HasValue)
            query = query.Where(m => m.MatchPattern == matchPattern.Value);
        var data = await query
            .GroupBy(m => new { m.CreatedAt.DayOfWeek, m.CreatedAt.Hour })
            .Select(g => new
            {
                DayOfWeek = g.Key.DayOfWeek,
                Hour = g.Key.Hour,
                Count = g.Count()
            })
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Hour)
            .ToListAsync();
        return Ok(data);
    }
    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends(
        [FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-days);
        var matchesByDay = await db.ArenaMatches
            .Where(m => m.CreatedAt >= since)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Matches = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();
        var newTeamsByDay = await db.ArenaTeams
            .Where(t => t.UpdatedAt >= since)
            .GroupBy(t => t.UpdatedAt.Date)
            .Select(g => new { Date = g.Key, Teams = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();
        var newPlayersByDay = await db.ArenaPlayers
            .Where(p => p.UpdatedAt >= since)
            .GroupBy(p => p.UpdatedAt.Date)
            .Select(g => new { Date = g.Key, Players = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();
        var teamsDict = newTeamsByDay.ToDictionary(x => x.Date, x => x.Teams);
        var playersDict = newPlayersByDay.ToDictionary(x => x.Date, x => x.Players);
        var allDates = matchesByDay.Select(x => x.Date)
            .Union(newTeamsByDay.Select(x => x.Date))
            .Union(newPlayersByDay.Select(x => x.Date))
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        var trends = allDates.Select(d => new
        {
            Date = d,
            Matches = matchesByDay.FirstOrDefault(x => x.Date == d)?.Matches ?? 0,
            Teams = teamsDict.GetValueOrDefault(d, 0),
            Players = playersDict.GetValueOrDefault(d, 0)
        }).ToList();
        return Ok(trends);
    }
}
