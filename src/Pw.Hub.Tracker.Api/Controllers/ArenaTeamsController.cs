using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/arena/teams")]
public class ArenaTeamsController(TrackerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? zoneId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ArenaTeams.AsQueryable();

        if (zoneId.HasValue)
            query = query.Where(t => t.ZoneId == zoneId.Value);

        var total = await query.CountAsync();

        var teams = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.CaptainId,
                t.ZoneId,
                t.Name,
                t.WeekResetTimestamp,
                t.LastVisiteTimestamp,
                t.UpdatedAt,
                MemberCount = t.Members.Count
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items = teams });
    }

    [HttpGet("{teamId:long}")]
    public async Task<IActionResult> GetById(long teamId)
    {
        var team = await db.ArenaTeams
            .Where(t => t.Id == teamId)
            .Select(t => new
            {
                t.Id,
                t.CaptainId,
                t.ZoneId,
                t.Name,
                t.WeekResetTimestamp,
                t.LastVisiteTimestamp,
                t.UpdatedAt,
                Members = t.Members.Select(m => new
                {
                    m.PlayerId,
                    m.RewardMoneyInfo
                }).ToList(),
                BattleStats = db.ArenaBattleStats
                    .Where(s => s.EntityId == t.Id && s.EntityType == EntityType.Team)
                    .Select(s => new
                    {
                        s.MatchPattern,
                        s.Score,
                        s.WinCount,
                        s.BattleCount,
                        s.WeekBattleCount,
                        s.WeekWinCount,
                        s.WeekMaxScore,
                        s.Rank
                    }).ToList()
            })
            .FirstOrDefaultAsync();

        if (team is null)
            return NotFound();

        return Ok(team);
    }

    [HttpGet("{teamId:long}/members")]
    public async Task<IActionResult> GetMembers(long teamId)
    {
        var members = await db.ArenaTeamMembers
            .Where(m => m.TeamId == teamId)
            .Select(m => new
            {
                m.PlayerId,
                m.RewardMoneyInfo,
                Player = new
                {
                    m.Player.Id,
                    m.Player.Cls,
                    m.Player.LastBattleTimestamp,
                    m.Player.LastVisiteTimestamp,
                    BattleStats = db.ArenaBattleStats
                        .Where(s => s.EntityId == m.PlayerId && s.EntityType == EntityType.Player)
                        .Select(s => new
                        {
                            s.MatchPattern,
                            s.Score,
                            s.WinCount,
                            s.BattleCount,
                            s.WeekBattleCount,
                            s.WeekWinCount,
                            s.WeekMaxScore,
                            s.Rank
                        }).ToList()
                }
            })
            .ToListAsync();

        return Ok(members);
    }

    [HttpGet("{teamId:long}/matches")]
    public async Task<IActionResult> GetMatches(
        long teamId,
        [FromQuery] int? matchPattern,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ArenaMatches
            .Where(m => m.TeamAId == teamId || m.TeamBId == teamId);

        if (matchPattern.HasValue)
            query = query.Where(m => m.MatchPattern == matchPattern.Value);

        var total = await query.CountAsync();

        var matches = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.MatchPattern,
                m.TeamAId,
                m.TeamBId,
                m.WinnerTeamId,
                m.LoserTeamId,
                m.TeamAScoreBefore,
                m.TeamAScoreAfter,
                m.TeamBScoreBefore,
                m.TeamBScoreAfter,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items = matches });
    }

    [HttpGet("{teamId:long}/score-history")]
    public async Task<IActionResult> GetScoreHistory(
        long teamId,
        [FromQuery] int? matchPattern,
        [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 1000);

        var query = db.ArenaScoreHistory
            .Where(h => h.EntityId == teamId && h.EntityType == EntityType.Team);

        if (matchPattern.HasValue)
            query = query.Where(h => h.MatchPattern == matchPattern.Value);

        var history = await query
            .OrderByDescending(h => h.RecordedAt)
            .Take(limit)
            .Select(h => new
            {
                h.MatchPattern,
                h.Score,
                h.WinCount,
                h.BattleCount,
                h.RecordedAt
            })
            .ToListAsync();

        return Ok(history);
    }
}
