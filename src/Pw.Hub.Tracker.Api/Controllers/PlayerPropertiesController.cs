using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Tracker.Infrastructure.Data;

namespace Pw.Hub.Tracker.Api.Controllers;

[ApiController]
[Route("api/players/properties")]
public class PlayerPropertiesController(TrackerDbContext db) : ControllerBase
{
    [HttpPost("by-ids")]
    public async Task<IActionResult> GetByIds([FromBody] long[] playerIds)
    {
        if (playerIds is not { Length: > 0 })
            return BadRequest("playerIds must not be empty");

        var properties = await db.PlayerProperties
            .Where(p => playerIds.Contains(p.PlayerId))
            .Select(p => new
            {
                p.PlayerId,
                PlayerName = db.ArenaPlayers.Where(a => a.Id == p.PlayerId).Select(a => a.Name).FirstOrDefault(),
                PlayerCls = db.ArenaPlayers.Where(a => a.Id == p.PlayerId).Select(a => (int?)a.Cls).FirstOrDefault(),
                p.Server,
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
                p.PeakGrade,
                p.UpdatedAt
            })
            .ToListAsync();

        return Ok(properties);
    }
}
