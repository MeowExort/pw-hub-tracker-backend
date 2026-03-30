using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/arena/players")]
public class ArenaPlayersController(TrackerDbContext db) : ControllerBase
{
    [HttpGet("{playerId:long}")]
    public async Task<IActionResult> GetById(long playerId)
    {
        var player = await db.ArenaPlayers
            .Where(p => p.Id == playerId)
            .Select(p => new
            {
                p.Id,
                p.Player.Name,
                p.TeamId,
                p.Player.Cls,
                p.Player.Gender,
                p.RewardMoney,
                p.WeekResetTimestamp,
                p.LastBattleTimestamp,
                p.LastVisiteTimestamp,
                p.UpdatedAt,
                BattleStats = db.ArenaBattleStats
                    .Where(s => s.EntityId == p.Id && s.EntityType == EntityType.Player)
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

        if (player is null)
            return NotFound();

        return Ok(player);
    }

    [HttpGet("{playerId:long}/matches")]
    public async Task<IActionResult> GetMatches(
        long playerId,
        [FromQuery] int? matchPattern,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ArenaMatchParticipants
            .Where(p => p.PlayerId == playerId);

        if (matchPattern.HasValue)
            query = query.Where(p => p.Match.MatchPattern == matchPattern.Value);

        var total = await query.CountAsync();

        var matches = await query
            .OrderByDescending(p => p.Match.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.MatchId,
                p.TeamId,
                p.PlayerCls,
                p.ScoreBefore,
                p.ScoreAfter,
                p.IsWinner,
                Match = new
                {
                    p.Match.MatchPattern,
                    p.Match.TeamAId,
                    p.Match.TeamBId,
                    p.Match.WinnerTeamId,
                    p.Match.CreatedAt
                }
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items = matches });
    }

    [HttpGet("{playerId:long}/score-history")]
    public async Task<IActionResult> GetScoreHistory(
        long playerId,
        [FromQuery] int? matchPattern,
        [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 1000);

        var query = db.ArenaScoreHistory
            .Where(h => h.EntityId == playerId && h.EntityType == EntityType.Player);

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
