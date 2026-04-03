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
            properties = await db.PlayerPropertyHistory
                .Where(pp => pp.PlayerId == playerId && pp.Server == server)
                .GroupBy(pp => new { pp.PlayerId, pp.Server })
                .Select(g => new
                {
                    g.Key.PlayerId,
                    PlayerCls = (int?)db.Players.Where(pl => pl.Id == g.Key.PlayerId && pl.Server == g.Key.Server).Select(pl => pl.Cls).FirstOrDefault(),
                    PlayerName = db.Players.Where(pl => pl.Id == g.Key.PlayerId && pl.Server == g.Key.Server).Select(pl => pl.Name).FirstOrDefault(),
                    g.Key.Server,
                    Hp = g.Max(x => x.Hp),
                    Mp = g.Max(x => x.Mp),
                    DamageLow = g.Max(x => x.DamageLow),
                    DamageHigh = g.Max(x => x.DamageHigh),
                    DamageMagicLow = g.Max(x => x.DamageMagicLow),
                    DamageMagicHigh = g.Max(x => x.DamageMagicHigh),
                    Defense = g.Max(x => x.Defense),
                    Resistance = g.OrderByDescending(x => x.RecordedAt).Select(x => x.Resistance).FirstOrDefault(),
                    Attack = g.Max(x => x.Attack),
                    Armor = g.Max(x => x.Armor),
                    AttackSpeed = g.Max(x => x.AttackSpeed),
                    RunSpeed = g.Max(x => x.RunSpeed),
                    AttackDegree = g.Max(x => x.AttackDegree),
                    DefendDegree = g.Max(x => x.DefendDegree),
                    CritRate = g.Max(x => x.CritRate),
                    DamageReduce = g.Max(x => x.DamageReduce),
                    Prayspeed = g.Max(x => x.Prayspeed),
                    CritDamageBonus = g.Max(x => x.CritDamageBonus),
                    InvisibleDegree = g.Max(x => x.InvisibleDegree),
                    AntiInvisibleDegree = g.Max(x => x.AntiInvisibleDegree),
                    Vigour = g.Max(x => x.Vigour),
                    AntiDefenseDegree = g.Max(x => x.AntiDefenseDegree),
                    AntiResistanceDegree = g.Max(x => x.AntiResistanceDegree),
                    PeakGrade = g.Max(x => x.PeakGrade),
                    UpdatedAt = g.Max(x => x.RecordedAt)
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
