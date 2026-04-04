using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/arena/matches")]
public class ArenaMatchesController(TrackerDbContext db, ILogger<ArenaMatchesController> logger) : ControllerBase
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
                TeamAName = m.TeamA.Name,
                m.TeamBId,
                TeamBName = m.TeamB.Name,
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
                    p.PlayerServer,
                    PlayerName = p.Player.Player.Name,
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

    [HttpGet("{matchId:long}/related")]
    public async Task<IActionResult> GetRelated(long matchId)
    {
        // Находим корневой матч (либо сам является оригиналом, либо ссылается на него)
        var match = await db.ArenaMatches
            .Where(m => m.Id == matchId)
            .Select(m => new { RootId = m.OriginalMatchId ?? m.Id })
            .FirstOrDefaultAsync();

        if (match is null)
            return NotFound();

        var related = await db.ArenaMatches
            .Where(m => m.Id == match.RootId || m.OriginalMatchId == match.RootId)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.MatchPattern,
                m.TeamAId,
                TeamAName = m.TeamA.Name,
                m.TeamBId,
                TeamBName = m.TeamB.Name,
                m.WinnerTeamId,
                m.LoserTeamId,
                m.TeamAScoreBefore,
                m.TeamAScoreAfter,
                m.TeamBScoreBefore,
                m.TeamBScoreAfter,
                m.OriginalMatchId,
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(related);
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
                TeamAName = m.TeamA.Name,
                m.TeamBId,
                TeamBName = m.TeamB.Name,
                m.WinnerTeamId,
                m.LoserTeamId,
                m.TeamAScoreBefore,
                m.TeamAScoreAfter,
                m.TeamBScoreBefore,
                m.TeamBScoreAfter,
                TeamAMemberCount = m.Participants.Count(p => p.TeamId == m.TeamAId),
                TeamBMemberCount = m.Participants.Count(p => p.TeamId == m.TeamBId),
                m.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items = matches });
    }
}
