using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Domain.Entities;
using Pw.Hub.Tracker.Infrastructure.Data;
namespace Pw.Hub.Tracker.Api.Controllers;
[ApiController]
[Route("api/analytics/players")]
public class PlayerCharacteristicsController(TrackerDbContext db) : ControllerBase
{
    [HttpGet("{server}/{playerId:long}/card")]
    public async Task<IActionResult> GetPlayerCard(string server, long playerId)
    {
        var props = await db.PlayerProperties
            .Where(p => p.PlayerId == playerId && p.Server == server)
            .FirstOrDefaultAsync();
        if (props is null)
            return NotFound();
        var player = await db.Players
            .Where(p => p.Id == playerId && p.Server == server)
            .Select(p => new { p.Name, p.Cls, p.Gender })
            .FirstOrDefaultAsync();
        var battleStats = await db.ArenaBattleStats
            .Where(s => s.EntityId == playerId && s.Server == server && s.EntityType == EntityType.Player)
            .Select(s => new
            {
                s.MatchPattern,
                s.Score,
                s.WinCount,
                s.BattleCount,
                WinRate = s.BattleCount > 0
                    ? Math.Round((double)s.WinCount / s.BattleCount * 100, 2)
                    : 0
            })
            .ToListAsync();
        return Ok(new
        {
            PlayerId = playerId,
            Server = server,
            player?.Name,
            player?.Cls,
            player?.Gender,
            Properties = new
            {
                props.Hp,
                props.Mp,
                props.DamageLow,
                props.DamageHigh,
                props.DamageMagicLow,
                props.DamageMagicHigh,
                props.Defense,
                props.Resistance,
                props.Attack,
                props.Armor,
                props.AttackSpeed,
                props.RunSpeed,
                props.AttackDegree,
                props.DefendDegree,
                props.CritRate,
                props.DamageReduce,
                props.Prayspeed,
                props.CritDamageBonus,
                props.InvisibleDegree,
                props.AntiInvisibleDegree,
                props.Vigour,
                props.AntiDefenseDegree,
                props.AntiResistanceDegree,
                props.PeakGrade
            },
            BattleStats = battleStats
        });
    }
    [HttpGet("compare")]
    public async Task<IActionResult> Compare(
        [FromQuery] string player1Server,
        [FromQuery] long player1Id,
        [FromQuery] string player2Server,
        [FromQuery] long player2Id)
    {
        var props = await db.PlayerProperties
            .Where(p =>
                (p.PlayerId == player1Id && p.Server == player1Server) ||
                (p.PlayerId == player2Id && p.Server == player2Server))
            .Join(db.Players,
                pp => new { Id = pp.PlayerId, pp.Server },
                pl => new { pl.Id, pl.Server },
                (pp, pl) => new
                {
                    pp.PlayerId,
                    pp.Server,
                    pl.Name,
                    pl.Cls,
                    pp.Hp,
                    pp.Mp,
                    pp.DamageLow,
                    pp.DamageHigh,
                    pp.DamageMagicLow,
                    pp.DamageMagicHigh,
                    pp.Defense,
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
                    pp.PeakGrade
                })
            .ToListAsync();
        if (props.Count < 2)
            return NotFound("One or both players not found");
        return Ok(props);
    }
    [HttpGet("{server}/{playerId:long}/property-history")]
    public async Task<IActionResult> GetPropertyHistory(
        string server,
        long playerId,
        [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var history = await db.PlayerPropertyHistory
            .Where(h => h.PlayerId == playerId && h.Server == server)
            .OrderByDescending(h => h.RecordedAt)
            .Take(limit)
            .Select(h => new
            {
                h.Hp,
                h.Mp,
                h.DamageLow,
                h.DamageHigh,
                h.DamageMagicLow,
                h.DamageMagicHigh,
                h.Defense,
                h.Attack,
                h.Armor,
                h.AttackSpeed,
                h.RunSpeed,
                h.AttackDegree,
                h.DefendDegree,
                h.CritRate,
                h.DamageReduce,
                h.Prayspeed,
                h.CritDamageBonus,
                h.InvisibleDegree,
                h.AntiInvisibleDegree,
                h.Vigour,
                h.AntiDefenseDegree,
                h.AntiResistanceDegree,
                h.PeakGrade,
                h.RecordedAt
            })
            .ToListAsync();
        return Ok(history);
    }
    [HttpGet("stats-distribution")]
    public async Task<IActionResult> GetStatsDistribution([FromQuery] string stat = "Hp")
    {
        var validStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hp", "Mp", "Attack", "Defense", "Armor", "DamageLow", "DamageHigh",
            "DamageMagicLow", "DamageMagicHigh", "AttackSpeed", "CritRate",
            "DamageReduce", "PeakGrade", "Vigour", "AttackDegree", "DefendDegree"
        };
        if (!validStats.Contains(stat))
            return BadRequest($"Invalid stat. Valid values: {string.Join(", ", validStats)}");
        var data = await db.PlayerProperties
            .Join(db.Players,
                pp => new { Id = pp.PlayerId, pp.Server },
                pl => new { pl.Id, pl.Server },
                (pp, pl) => new { pp, pl.Cls })
            .ToListAsync();
        var property = typeof(PlayerProperty).GetProperty(stat,
            System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (property is null)
            return BadRequest("Invalid stat");
        var distribution = data
            .GroupBy(x => x.Cls)
            .Select(g => new
            {
                Cls = g.Key,
                Count = g.Count(),
                Min = g.Min(x => Convert.ToDouble(property.GetValue(x.pp))),
                Max = g.Max(x => Convert.ToDouble(property.GetValue(x.pp))),
                Average = Math.Round(g.Average(x => Convert.ToDouble(property.GetValue(x.pp))), 2)
            })
            .OrderBy(x => x.Cls)
            .ToList();
        return Ok(distribution);
    }
    [HttpGet("winrate-correlation")]
    public async Task<IActionResult> GetWinrateCorrelation(
        [FromQuery] string stat = "Attack",
        [FromQuery] int? matchPattern = null)
    {
        var validStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Hp", "Mp", "Attack", "Defense", "Armor", "DamageLow", "DamageHigh",
            "CritRate", "PeakGrade", "Vigour", "AttackDegree", "DefendDegree"
        };
        if (!validStats.Contains(stat))
            return BadRequest($"Invalid stat. Valid values: {string.Join(", ", validStats)}");
        var statsQuery = db.ArenaBattleStats
            .Where(s => s.EntityType == EntityType.Player && s.BattleCount > 0);
        if (matchPattern.HasValue)
            statsQuery = statsQuery.Where(s => s.MatchPattern == matchPattern.Value);
        var battleData = await statsQuery
            .GroupBy(s => new { s.EntityId, s.Server })
            .Select(g => new
            {
                g.Key.EntityId,
                g.Key.Server,
                TotalWins = g.Sum(s => s.WinCount),
                TotalBattles = g.Sum(s => s.BattleCount)
            })
            .ToListAsync();
        var props = await db.PlayerProperties.ToListAsync();
        var propsDict = props.ToDictionary(p => (p.PlayerId, p.Server));
        var property = typeof(PlayerProperty).GetProperty(stat,
            System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (property is null)
            return BadRequest("Invalid stat");
        var result = battleData
            .Where(b => propsDict.ContainsKey((b.EntityId, b.Server)))
            .Select(b =>
            {
                var pp = propsDict[(b.EntityId, b.Server)];
                return new
                {
                    PlayerId = b.EntityId,
                    b.Server,
                    StatValue = Convert.ToDouble(property.GetValue(pp)),
                    WinRate = Math.Round((double)b.TotalWins / b.TotalBattles * 100, 2)
                };
            })
            .OrderBy(x => x.StatValue)
            .ToList();
        return Ok(result);
    }
}
