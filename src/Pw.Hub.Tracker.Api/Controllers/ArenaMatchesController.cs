using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/arena/matches")]
public class ArenaMatchesController(TrackerDbContext db) : ControllerBase
{
    [HttpGet("{matchId:long}")]
    public async Task<IActionResult> GetById(long matchId)
    {
        var match = await db.ArenaMatches
            .Where(m => m.Id == matchId)
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
                m.CreatedAt,
                Participants = m.Participants.Select(p => new
                {
                    p.PlayerId,
                    p.TeamId,
                    p.PlayerCls,
                    p.ScoreBefore,
                    p.ScoreAfter,
                    p.IsWinner
                })
            })
            .FirstOrDefaultAsync();

        if (match is null)
            return NotFound();

        return Ok(match);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? matchPattern,
        [FromQuery] long? teamId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ArenaMatches.AsQueryable();

        if (matchPattern.HasValue)
            query = query.Where(m => m.MatchPattern == matchPattern.Value);

        if (teamId.HasValue)
            query = query.Where(m => m.TeamAId == teamId.Value || m.TeamBId == teamId.Value);

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
}
