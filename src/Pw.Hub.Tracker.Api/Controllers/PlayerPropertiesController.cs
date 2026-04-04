using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

public class PlayerRequest
{
    public long Id { get; set; }
    public string Server { get; set; }
}

[ApiController]
[Route("api/players/properties")]
public class PlayerPropertiesController(TrackerDbContext db) : ControllerBase
{
    [HttpPost("by-ids")]
    public async Task<IActionResult> GetByIds([FromBody] PlayerRequest[] players)
    {
        if (players is not { Length: > 0 })
            return BadRequest("playerIds must not be empty");

        // Группируем запросы по серверу для оптимизации
        var results = new List<object>();

        var playerIds = players.Select(p => p.Id).Distinct().ToList();
        var playerServers = players.Select(p => p.Server).Distinct().ToList();
        var playerKeySet = players.Select(p => (p.Id, p.Server)).ToHashSet();

        var matchedProps = (await db.PlayerMaxStats
            .Where(pp => playerIds.Contains(pp.PlayerId) && playerServers.Contains(pp.Server))
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
            .ToListAsync())
            .Where(p => playerKeySet.Contains((p.PlayerId, p.Server)))
            .ToList();

        results.AddRange(matchedProps);

        return Ok(results);
    }

    [HttpGet("max")]
    public async Task<IActionResult> GetMaxProperties()
    {
        var result = await db.PlayerMaxStats
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Hp = g.Max(x => x.Hp),
                Mp = g.Max(x => x.Mp),
                DamageLow = g.Max(x => x.DamageLow),
                DamageHigh = g.Max(x => x.DamageHigh),
                DamageMagicLow = g.Max(x => x.DamageMagicLow),
                DamageMagicHigh = g.Max(x => x.DamageMagicHigh),
                Defense = g.Max(x => x.Defense),
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
            })
            .FirstOrDefaultAsync();

        return Ok(result);
    }
}
