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
                TeamId = (long?)p.TeamId,
                TeamName = (string?)p.Team.Name,
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
        {
            var fallbackPlayer = await db.Players
                .Where(p => p.Id == playerId && p.Server == server)
                .Select(p => new
                {
                    p.Id,
                    Server = p.Server,
                    p.Name,
                    TeamId = (long?)null,
                    TeamName = (string?)null,
                    p.Cls,
                    p.Gender,
                    RewardMoney = 0L,
                    WeekResetTimestamp = 0L,
                    LastBattleTimestamp = 0L,
                    LastVisiteTimestamp = 0L,
                    p.UpdatedAt,
                    BattleStats = new List<object>()
                })
                .FirstOrDefaultAsync();

            if (fallbackPlayer is null)
                return NotFound();

            object? fallbackProperties = null;
            if (includeList.Contains("properties"))
            {
                fallbackProperties = await db.PlayerMaxStats
                    .Where(pp => pp.PlayerId == playerId && pp.Server == server)
                    .Join(db.Players, pp => new { Id = pp.PlayerId, pp.Server }, pl => new { pl.Id, pl.Server }, (pp, pl) => new
                    {
                        pp.PlayerId,
                        PlayerCls = (int?)pl.Cls,
                        PlayerName = pl.Name,
                        pp.Server,
                        pp.Hp,
                        pp.Mp,
                        pp.DamageLow,
                        pp.DamageHigh,
                        pp.DamageMagicLow,
                        pp.DamageMagicHigh,
                        pp.Defense,
                        pp.Resistance,
                        pp.Attack,
                        pp.Armor,
                        pp.AttackSpeed,
                        pp.RunSpeed,
                        pp.AttackDegree,
                        pp.DefendDegree,
                        pp.CritRate,
                        pp.DamageReduce,
                        pp.Prayspeed,
                        pp.CritDamageBonus,
                        pp.InvisibleDegree,
                        pp.AntiInvisibleDegree,
                        pp.Vigour,
                        pp.AntiDefenseDegree,
                        pp.AntiResistanceDegree,
                        pp.PeakGrade,
                        pp.UpdatedAt
                    })
                    .FirstOrDefaultAsync();
            }

            return Ok(new
            {
                fallbackPlayer.Id,
                fallbackPlayer.Server,
                fallbackPlayer.Name,
                fallbackPlayer.TeamId,
                fallbackPlayer.TeamName,
                fallbackPlayer.Cls,
                fallbackPlayer.Gender,
                fallbackPlayer.RewardMoney,
                fallbackPlayer.WeekResetTimestamp,
                fallbackPlayer.LastBattleTimestamp,
                fallbackPlayer.LastVisiteTimestamp,
                fallbackPlayer.UpdatedAt,
                fallbackPlayer.BattleStats,
                Properties = fallbackProperties,
                ScoreHistory = (object?)null,
                Team = (object?)null
            });
        }

        object? properties = null;
        if (includeList.Contains("properties"))
        {
            properties = await db.PlayerMaxStats
                .Where(pp => pp.PlayerId == playerId && pp.Server == server)
                .Join(db.Players, pp => new { Id = pp.PlayerId, pp.Server }, pl => new { pl.Id, pl.Server }, (pp, pl) => new
                {
                    pp.PlayerId,
                    PlayerCls = (int?)pl.Cls,
                    PlayerName = pl.Name,
                    pp.Server,
                    pp.Hp,
                    pp.Mp,
                    pp.DamageLow,
                    pp.DamageHigh,
                    pp.DamageMagicLow,
                    pp.DamageMagicHigh,
                    pp.Defense,
                    pp.Resistance,
                    pp.Attack,
                    pp.Armor,
                    pp.AttackSpeed,
                    pp.RunSpeed,
                    pp.AttackDegree,
                    pp.DefendDegree,
                    pp.CritRate,
                    pp.DamageReduce,
                    pp.Prayspeed,
                    pp.CritDamageBonus,
                    pp.InvisibleDegree,
                    pp.AntiInvisibleDegree,
                    pp.Vigour,
                    pp.AntiDefenseDegree,
                    pp.AntiResistanceDegree,
                    pp.PeakGrade,
                    pp.UpdatedAt
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
