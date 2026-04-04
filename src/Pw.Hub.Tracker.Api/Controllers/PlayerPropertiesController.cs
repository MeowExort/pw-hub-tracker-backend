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

        var playerKeys = players.Select(p => new { p.Id, p.Server }).ToList();
        var matchedProps = await db.PlayerMaxStats
            .Where(pp => playerKeys.Any(k => k.Id == pp.PlayerId && k.Server == pp.Server))
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
            .ToListAsync();

        results.AddRange(matchedProps);

        return Ok(results);
    }
}
