using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/arena/players")]
public class ArenaPlayersController(TrackerDbContext db) : ControllerBase
{
    [HttpGet("{server}/{playerId:long}")]
    public async Task<IActionResult> GetById(
        string server,
        long playerId,
        [FromQuery] string? include = null)
    {
        var includeList = (include ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLowerInvariant()).ToList();

        var playerQuery = db.ArenaPlayers
            .Where(p => p.Id == playerId && p.PlayerServer == server);

        var player = await playerQuery
            .Select(p => new
            {
                p.Id,
                Server = p.PlayerServer,
                p.Player.Name,
                p.TeamId,
                TeamName = p.Team.Name,
                p.Player.Cls,
                p.Player.Gender,
                p.RewardMoney,
                p.WeekResetTimestamp,
                p.LastBattleTimestamp,
                p.LastVisiteTimestamp,
                p.UpdatedAt,
                BattleStats = db.ArenaBattleStats
                    .Where(s => s.EntityId == p.Id && s.Server == p.PlayerServer && s.EntityType == EntityType.Player)
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

        object? properties = null;
        if (includeList.Contains("properties"))
        {
            properties = await db.PlayerProperties
                .Where(p => p.PlayerId == playerId && p.Server == server)
                .Select(p => new
                {
                    p.Hp,
                    p.Mp,
                    p.DamageLow,
                    p.DamageHigh,
                    p.DamageMagicLow,
                    p.DamageMagicHigh,
                    p.Defense,
                    p.Resistance,
                    p.Attack,
                    p.Armor,
                    p.AttackSpeed,
                    p.RunSpeed,
                    p.AttackDegree,
                    p.DefendDegree,
                    p.CritRate,
                    p.DamageReduce,
                    p.Prayspeed,
                    p.CritDamageBonus,
                    p.InvisibleDegree,
                    p.AntiInvisibleDegree,
                    p.Vigour,
                    p.AntiDefenseDegree,
                    p.AntiResistanceDegree,
                    p.PeakGrade
                })
                .FirstOrDefaultAsync();
        }

        object? scoreHistory = null;
        if (includeList.Contains("scorehistory"))
        {
            scoreHistory = await db.ArenaScoreHistory
                .Where(h => h.EntityId == playerId && h.Server == server && h.EntityType == EntityType.Player)
                .OrderByDescending(h => h.RecordedAt)
                .Take(200)
                .Select(h => new
                {
                    h.MatchPattern,
                    h.Score,
                    h.WinCount,
                    h.BattleCount,
                    h.RecordedAt
                })
                .ToListAsync();
        }

        object? team = null;
        if (includeList.Contains("team") && player.TeamId > 0)
        {
            team = await db.ArenaTeams
                .Where(t => t.Id == player.TeamId)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.ZoneId,
                    t.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        return Ok(new
        {
            player.Id,
            player.Server,
            player.Name,
            player.TeamId,
            player.TeamName,
            player.Cls,
            player.Gender,
            player.RewardMoney,
            player.WeekResetTimestamp,
            player.LastBattleTimestamp,
            player.LastVisiteTimestamp,
            player.UpdatedAt,
            player.BattleStats,
            Properties = properties,
            ScoreHistory = scoreHistory,
            Team = team
        });
    }

    [HttpGet("{server}/{playerId:long}/matches")]
    public async Task<IActionResult> GetMatches(
        string server,
        long playerId,
        [FromQuery] int? matchPattern,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.ArenaMatchParticipants
            .Where(p => p.PlayerId == playerId && p.PlayerServer == server);

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
                    TeamAName = p.Match.TeamA.Name,
                    p.Match.TeamBId,
                    TeamBName = p.Match.TeamB.Name,
                    p.Match.WinnerTeamId,
                    p.Match.CreatedAt
                }
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items = matches });
    }

    [HttpGet("{server}/{playerId:long}/score-history")]
    public async Task<IActionResult> GetScoreHistory(
        string server,
        long playerId,
        [FromQuery] int? matchPattern,
        [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 1000);

        var query = db.ArenaScoreHistory
            .Where(h => h.EntityId == playerId && h.Server == server && h.EntityType == EntityType.Player);

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
