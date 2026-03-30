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

        var properties = await db.PlayerPropertyHistory
            .Where(h => playerIds.Contains(h.PlayerId))
            .GroupBy(h => new { h.PlayerId, h.Server })
            .Select(g => new
            {
                g.Key.PlayerId,
                PlayerName = db.Players.Where(a => a.Id == g.Key.PlayerId).Select(a => a.Name).FirstOrDefault(),
                PlayerCls = db.Players.Where(a => a.Id == g.Key.PlayerId).Select(a => (int?)a.Cls).FirstOrDefault(),
                g.Key.Server,
                Hp = g.Max(h => h.Hp),
                Mp = g.Max(h => h.Mp),
                DamageLow = g.Max(h => h.DamageLow),
                DamageHigh = g.Max(h => h.DamageHigh),
                DamageMagicLow = g.Max(h => h.DamageMagicLow),
                DamageMagicHigh = g.Max(h => h.DamageMagicHigh),
                Defense = g.Max(h => h.Defense),
                Resistance = g.OrderByDescending(h => h.RecordedAt).Select(h => h.Resistance).First(),
                Attack = g.Max(h => h.Attack),
                Armor = g.Max(h => h.Armor),
                AttackSpeed = g.Max(h => h.AttackSpeed),
                RunSpeed = g.Max(h => h.RunSpeed),
                AttackDegree = g.Max(h => h.AttackDegree),
                DefendDegree = g.Max(h => h.DefendDegree),
                CritRate = g.Max(h => h.CritRate),
                DamageReduce = g.Max(h => h.DamageReduce),
                Prayspeed = g.Max(h => h.Prayspeed),
                CritDamageBonus = g.Max(h => h.CritDamageBonus),
                InvisibleDegree = g.Max(h => h.InvisibleDegree),
                AntiInvisibleDegree = g.Max(h => h.AntiInvisibleDegree),
                Vigour = g.Max(h => h.Vigour),
                AntiDefenseDegree = g.Max(h => h.AntiDefenseDegree),
                AntiResistanceDegree = g.Max(h => h.AntiResistanceDegree),
                PeakGrade = g.Max(h => h.PeakGrade),
                UpdatedAt = g.Max(h => h.RecordedAt)
            })
            .ToListAsync();

        return Ok(properties);
    }
}
